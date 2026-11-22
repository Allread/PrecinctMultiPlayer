#if IL2CPP
using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppFallenTreeGames.CharacterController;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrecinctMP
{
    /// <summary>
    /// Hôte : lit les crimes en cours et leurs suspects pour les envoyer aux clients.
    ///   AmbientCrimeManager.CurrentAmbientCrimes()  -> crimes (type, état, position)
    ///   AmbientCrime_Base.GetActiveTargetTracker()  -> ObjectiveTargetTracker
    ///   ObjectiveTargetTracker.GetObjectiveTargets()-> suspects (Character + statut)
    /// </summary>
    public class HostCrimes
    {
        private const float IncidentInterval = 1f;
        private const float SuspectInterval = 0.1f;

        private AmbientCrimeManager _crimes;
        private float _nextLookup, _nextIncidents, _nextSuspects;
        private bool _warned;

        // Ids réseau stables : pointeur de l'objet du jeu -> petit numéro
        private readonly Dictionary<IntPtr, ushort> _ids = new Dictionary<IntPtr, ushort>();
        private ushort _nextId = 1;

        // Cache par suspect (Animator, nom du modèle)
        private class SuspectCache { public AnimatorSync Anim; public string Model; public float NextModelCheck; }
        private readonly Dictionary<IntPtr, SuspectCache> _cache = new Dictionary<IntPtr, SuspectCache>();

        private readonly List<IncidentInfo> _incidents = new List<IncidentInfo>();
        private readonly List<SuspectState> _suspects = new List<SuspectState>();

        public int IncidentCount => _incidents.Count;
        public int SuspectCount => _suspects.Count;

        public void Tick(NetSession session)
        {
            if (!session.HasClients) return;
            float now = Time.unscaledTime;
            if (now < _nextSuspects) return;
            _nextSuspects = now + SuspectInterval;

            if (!Find()) return;
            try { Capture(now >= _nextIncidents); }
            catch (Exception e) { Warn("lecture des crimes", e); return; }

            if (now >= _nextIncidents)
            {
                _nextIncidents = now + IncidentInterval;
                session.SendIncidents(_incidents);
            }
            session.SendSuspects(_suspects);
        }

        public void Reset()
        {
            _ids.Clear();
            _cache.Clear();
            _incidents.Clear();
            _suspects.Clear();
            _nextId = 1;
        }

        private bool Find()
        {
            if (_crimes != null) return true;
            if (Time.unscaledTime < _nextLookup) return false;
            _nextLookup = Time.unscaledTime + 2f;
            try { _crimes = Object.FindObjectOfType<AmbientCrimeManager>(); }
            catch (Exception e) { Warn("recherche AmbientCrimeManager", e); }
            return _crimes != null;
        }

        private void Capture(bool withIncidents)
        {
            _incidents.Clear();
            _suspects.Clear();

            var list = _crimes.CurrentAmbientCrimes();
            if (list == null) return;

            for (int i = 0; i < list.Count && _incidents.Count < SharedWorldProtocol.MaxIncidents; i++)
            {
                var crime = list[i];
                if (crime == null) continue;
                var state = crime.CurrentCrimeState();
                if (state == AmbientCrimeState.Inactive) continue;

                ushort crimeId = IdOf(crime.Pointer);
                var info = new IncidentInfo
                {
                    Id = crimeId,
                    CrimeType = (short)crime.GetAmbientCrimeType(),
                    State = (byte)state,
                    HostOnIt = SafeBool(() => crime.IsPlayersActiveObjective()),
                };

                bool hasPos = false;
                var tracker = crime.GetActiveTargetTracker();
                if (tracker != null)
                {
                    info.Suspects = (byte)Mathf.Clamp(tracker.GetTotalCharactersAdded(), 0, 255);
                    info.Resolved = (byte)Mathf.Clamp(tracker.GetCharactersResolved(), 0, 255);

                    var targets = tracker.GetObjectiveTargets();
                    if (targets != null)
                    {
                        for (int t = 0; t < targets.Count; t++)
                        {
                            var target = targets[t];
                            var ch = target?.Character();
                            if (ch == null) continue;
                            var status = target.GetStatus();
                            if (status == ObjectiveTarget.Status.Escaped) continue;

                            if (!hasPos) { info.Pos = ch.transform.position; hasPos = true; }
                            if (_suspects.Count < SharedWorldProtocol.MaxSuspects)
                                _suspects.Add(ReadSuspect(ch, crimeId, (byte)status));
                        }
                    }

                    if (!hasPos)
                    {
                        var loc = tracker.GetTargetLocation();
                        if (loc != null) { info.Pos = loc.position; hasPos = true; }
                    }
                }

                if (!hasPos)
                {
                    var primary = crime.GetPrimaryCharacter();
                    if (primary != null) { info.Pos = primary.transform.position; hasPos = true; }
                }
                if (!hasPos)
                {
                    var point = crime.GetAttachedAmbientCrimePoint();
                    if (point != null) { info.Pos = point.transform.position; hasPos = true; }
                }
                if (hasPos) _incidents.Add(info);
            }

            if (withIncidents) Prune();
        }

        private SuspectState ReadSuspect(Character ch, ushort crimeId, byte status)
        {
            var go = ch.gameObject;
            var tr = ch.transform;
            var s = new SuspectState
            {
                Id = IdOf(ch.Pointer),
                IncidentId = crimeId,
                Status = status,
                Pos = tr.position,
                Rot = tr.rotation,
            };

            if (!_cache.TryGetValue(ch.Pointer, out var c)) { c = new SuspectCache(); _cache[ch.Pointer] = c; }

            // Nom du modèle (apparence) : vérifié de temps en temps (changement de tenue, etc.)
            if (Time.unscaledTime >= c.NextModelCheck)
            {
                c.NextModelCheck = Time.unscaledTime + 3f;
                c.Model = ModelName(go);
            }
            s.Model = c.Model;

            // Véhicule ?
            VehicleController vc = null;
            try
            {
                var def = CharacterDef.GetCharacterDefFromGameObject(go);
                var vgo = def?.GetVehicleCurrentlyInside();
                if (vgo != null) vc = vgo.GetComponentInParent<VehicleController>();
            }
            catch { /* pas de véhicule */ }

            if (vc != null)
            {
                s.InVehicle = true;
                var rb = vc.GetRigidbody();
                var vt = rb != null ? rb.transform : vc.transform;
                s.VehPos = vt.position;
                s.VehRot = vt.rotation;
                s.VehVel = rb != null ? rb.velocity : Vector3.zero;
                s.VehicleType = (int)vc.GetVehicleType();
                return s;
            }

            // Animation (à pied)
            if (c.Anim == null || c.Anim.Animator == null)
            {
                var a = AnimatorSync.FindBodyAnimator(go);
                c.Anim = a != null ? new AnimatorSync(a) : null;
            }
            if (c.Anim != null)
            {
                try
                {
                    s.Anim = c.Anim.Capture(out s.AnimParamCount);
                    s.AnimStates = c.Anim.CaptureStates();
                }
                catch { c.Anim = null; }
            }
            return s;
        }

        /// <summary>Nom du modèle 3D actif (sert au client à retrouver la même apparence).</summary>
        public static string ModelName(GameObject character)
        {
            try
            {
                var mm = character.GetComponentInChildren<CharacterModelManager>(true);
                var active = mm != null ? mm.GetActiveGameObject() : null;
                if (active != null) return active.name;
            }
            catch { }
            var smr = character.GetComponentInChildren<SkinnedMeshRenderer>();
            return smr != null ? smr.name : "";
        }

        private ushort IdOf(IntPtr ptr)
        {
            if (_ids.TryGetValue(ptr, out var id)) return id;
            id = _nextId++;
            if (_nextId == 0) _nextId = 1;
            _ids[ptr] = id;
            return id;
        }

        /// <summary>Oublie les objets disparus (évite que les dictionnaires grossissent indéfiniment).</summary>
        private void Prune()
        {
            if (_ids.Count < 512) return;
            var keep = new HashSet<ushort>();
            foreach (var i in _incidents) keep.Add(i.Id);
            foreach (var s in _suspects) keep.Add(s.Id);
            var dead = new List<IntPtr>();
            foreach (var kv in _ids) if (!keep.Contains(kv.Value)) dead.Add(kv.Key);
            foreach (var p in dead) { _ids.Remove(p); _cache.Remove(p); }
        }

        private static bool SafeBool(Func<bool> f) { try { return f(); } catch { return false; } }

        private void Warn(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            MelonLogger.Warning($"[MP] Partage des crimes indisponible ({what}) : {e.Message}");
        }
    }
}
#endif
