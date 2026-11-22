using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// Joueur local et sa voiture.
    /// Priorité à l'API du jeu (PlayerManager) ; repli sur des heuristiques Unity
    /// si elle n'est pas disponible (menu, mise à jour du jeu...).
    /// </summary>
    public class LocalPlayer
    {
        public Transform Player { get; private set; }
        public Rigidbody Vehicle { get; private set; }
        public string VehicleName { get; private set; }
        public bool UsingGameApi { get; private set; }

        /// <summary>Animator du corps du joueur local (sert aussi de modèle pour les joueurs distants).</summary>
        public Animator BodyAnimator => _animSync?.Animator;
        private AnimatorSync _animSync;
        private float _nextAnimSearch;

#if IL2CPP
        private Il2Cpp.VehicleController _vc;
#endif
        private float _nextSearch;
        private uint _seq;

        public void Update(string forcedName, float minVehicleMass)
        {
#if IL2CPP
            if (GameApi.Available)
            {
                UsingGameApi = true;
                var go = GameApi.PlayerObject();
                Player = go != null ? go.transform : null;
                _vc = GameApi.PlayerVehicle();
                Vehicle = _vc != null ? _vc.GetRigidbody() : null;
                VehicleName = _vc != null ? GameApi.VehicleTypeName((int)_vc.GetVehicleType()) : null;
                UpdateAnimator();
                return;
            }
            _vc = null;
#endif
            UsingGameApi = false;
            if (Player == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 2f;
                Player = FindPlayer(forcedName);
            }
            Vehicle = Player != null ? FindVehicle(Player, minVehicleMass) : null;
            VehicleName = Vehicle != null ? Vehicle.name : null;
            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            if (Player == null) { _animSync = null; return; }
            if (_animSync != null && _animSync.Animator != null) return;
            if (Time.unscaledTime < _nextAnimSearch) return;
            _nextAnimSearch = Time.unscaledTime + 2f;
            try
            {
                var a = AnimatorSync.FindBodyAnimator(Player.gameObject);
                _animSync = a != null ? new AnimatorSync(a) : null;
                if (_animSync != null)
                    MelonLoader.MelonLogger.Msg($"[MP] Animator du joueur : {a.name} ({_animSync.ParamCount} paramètres, {a.layerCount} calques)");
            }
            catch (System.Exception e)
            {
                _animSync = null;
                MelonLoader.MelonLogger.Warning("[MP] Animator du joueur illisible : " + e.Message);
            }
        }

        public bool TryGetState(out PlayerState s)
        {
            s = default;
            if (Player == null) return false;
            s.Seq = ++_seq;
            s.Pos = Player.position;
            s.Rot = Player.rotation;
            bool onFoot = Vehicle == null;
#if IL2CPP
            onFoot &= _vc == null;
#endif
            if (_animSync != null && onFoot)
            {
                try
                {
                    s.Anim = _animSync.Capture(out s.AnimParamCount);
                    s.AnimStates = _animSync.CaptureStates();
                }
                catch { _animSync = null; }
            }

#if IL2CPP
            if (_vc != null)
            {
                s.InVehicle = true;
                GameApi.ReadVehicle(_vc, ref s);
                return true;
            }
#endif
            if (Vehicle != null)
            {
                s.InVehicle = true;
                s.VehPos = Vehicle.position;
                s.VehRot = Vehicle.rotation;
                s.VehVel = Vehicle.velocity;
            }
            return true;
        }

        private static Transform FindPlayer(string forcedName)
        {
            if (!string.IsNullOrEmpty(forcedName))
            {
                var go = GameObject.Find(forcedName);
                if (go != null) return go.transform;
            }
            try
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null) return tagged.transform;
            }
            catch (System.Exception) { /* tag absent */ }
            return null;
        }

        private static Rigidbody FindVehicle(Transform player, float minMass)
        {
            for (var t = player.parent; t != null; t = t.parent)
            {
                var rb = t.GetComponent<Rigidbody>();
                if (rb != null && rb.mass >= minMass) return rb;
            }
            return null;
        }
    }
}
