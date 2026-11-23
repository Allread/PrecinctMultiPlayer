#if IL2CPP
using System;
using Il2Cpp;
using Il2CppFallenTreeGames.CharacterController;
using MelonLoader;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// Hôte : exécute sur ses vrais PNJ les actions demandées par les clients.
    /// Le personnage de l'hôte sert d'« auteur » pour le jeu (dégâts, arrestation),
    /// mais son propre personnage ne bouge pas.
    ///
    ///   Ordre de se rendre : AISurrenderModule.SurrenderIfStateAllows()
    ///   Plaquage           : Character.OnReceiveMeleeAttack(Damage + ragdoll)
    ///   Menotter           : AISurrenderModule.SetRestrained + AddHandcuffs, statut de la cible
    ///   Fouiller           : CharacterStopAndSearchInfo (objets illégaux, arme)
    ///   Tir                : Character.OnReceiveProjectileAttack(Damage)
    ///
    /// L'état résultant (se rend, menotté, à terre, mort) repart vers tout le monde
    /// via le flux normal des suspects.
    /// </summary>
    public class HostActions
    {
        // Distances max (m) entre le client et le suspect, vérifiées par l'hôte
        private const float OrderRange = 25f, TackleRange = 4f, RestrainRange = 3f, SearchRange = 3f, ShootRange = 150f;

        private readonly HostCrimes _crimes;
        private readonly Func<byte, Vector3?> _clientPos;

        public HostActions(HostCrimes crimes, Func<byte, Vector3?> clientPosition)
        {
            _crimes = crimes;
            _clientPos = clientPosition;
        }

        public ActionResult Execute(byte clientId, ActionRequest req)
        {
            var res = new ActionResult { Action = req.Action, SuspectId = req.SuspectId };
            try
            {
                if (!_crimes.TryGetLive(req.SuspectId, out var live))
                    return Fail(res, "Suspect introuvable chez l'hôte.");

                var npc = live.Character;
                var npcPos = npc.transform.position;

                // L'hôte vérifie la distance avec la position qu'il connaît du client
                var cp = _clientPos(clientId);
                if (cp.HasValue)
                {
                    float d = Vector3.Distance(cp.Value, npcPos);
                    if (d > RangeFor(req.Action)) return Fail(res, $"Trop loin du suspect ({d:0} m).");
                }

                var brain = live.Target?.GetAIBrain();
                var surrender = brain?.SurrenderModule();
                var host = GameApi.PlayerCharacter();

                switch (req.Action)
                {
                    case SuspectAction.OrderSurrender:
                        if (surrender == null) return Fail(res, "Ce suspect ne peut pas se rendre.");
                        if (surrender.IsSurrendering() || surrender.IsRestrained()) return Ok(res, "Le suspect se rend déjà.");
                        return surrender.SurrenderIfStateAllows()
                            ? Ok(res, "Le suspect lève les mains.")
                            : Fail(res, "Le suspect refuse de se rendre.");

                    case SuspectAction.Tackle:
                    {
                        var dmg = MakeDamage(host, npc, 5f, req.Dir, npcPos + Vector3.up, ragdoll: true);
                        npc.OnReceiveMeleeAttack(dmg, host, false);
                        return Ok(res, "Suspect plaqué au sol.");
                    }

                    case SuspectAction.Restrain:
                    {
                        if (surrender == null) return Fail(res, "Impossible de menotter ce suspect.");
                        if (surrender.IsRestrained()) return Ok(res, "Le suspect est déjà menotté.");
                        bool down = !npc.IsInValidSurrenderPosition() || brain.IsSurrenderingOrIncapacitated();
                        if (!surrender.IsSurrendering() && !down && !surrender.CanBeArrestedInCurrentState(false))
                            return Fail(res, "Le suspect doit se rendre ou être à terre.");

                        surrender.SetRestrained(true, host, false, false, false, false);
                        surrender.AddHandcuffs(false);
                        if (live.Target != null && live.Tracker != null
                            && live.Target.GetStatus() != ObjectiveTarget.Status.Restrained
                            && live.Target.GetStatus() != ObjectiveTarget.Status.Arrested)
                            live.Tracker.ChangeStatusOfObjectiveTarget(live.Target, ObjectiveTarget.Status.Restrained);
                        return Ok(res, "Suspect menotté.");
                    }

                    case SuspectAction.Search:
                    {
                        if (surrender != null && !surrender.IsSurrendering() && !surrender.IsRestrained())
                            return Fail(res, "Le suspect doit se rendre ou être menotté pour être fouillé.");
                        var info = npc.CharacterStopAndSearchInfo();
                        if (info == null) return Fail(res, "Fouille impossible.");
                        info.SetCarriedOutSearch();
                        bool weapon = info.HasFoundFirearm() || info.DoesCharacterHaveUnlicensedWeapon() || info.DoesCharacterHaveUnlicenseableWeapon();
                        bool illegal = info.DoesCharacterOrVehicleHaveAnyIllegalItem(false);
                        string found = weapon ? "Arme trouvée !" : illegal ? "Objets illégaux trouvés." : "Rien trouvé.";
                        return Ok(res, "Fouille : " + found);
                    }

                    case SuspectAction.Shoot:
                    {
                        float damage = req.Damage > 0f ? req.Damage : 35f;
                        var dmg = MakeDamage(host, npc, damage, req.Dir, req.HitPos, ragdoll: false);
                        npc.OnReceiveProjectileAttack(dmg, host, false);
                        return Ok(res, "Touché.");
                    }
                }
                return Fail(res, "Action inconnue.");
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[MP] Action {req.Action} sur suspect {req.SuspectId} impossible : {e.Message}");
                return Fail(res, "Action impossible (voir le log de l'hôte).");
            }
        }

        private static Damage MakeDamage(Character sender, Character receiver, float value, Vector3 dir, Vector3 hitPos, bool ragdoll)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = receiver.transform.position - (sender != null ? sender.transform.position : receiver.transform.position - receiver.transform.forward);
            dir.y = Mathf.Clamp(dir.y, -0.3f, 0.3f);
            dir.Normalize();

            var d = new Damage();
            d._damageValue = value;
            d._activateRagdoll = ragdoll;
            // _senderCharacter/_receiverCharacter sont des CharacterReference (struct interne
            // non constructible depuis le mod) : l'attaquant est passé directement à OnReceive*Attack.
            d._senderTransform = sender != null ? sender.transform : null;
            d._receiverTransform = receiver.transform;
            d._direction = dir;
            d._directionValid = true;
            d._forceDir = dir;
            d._forceDirValid = true;
            d._hitPosition = hitPos;
            d._hitPositionValid = true;
            return d;
        }

        private static float RangeFor(SuspectAction a) => a switch
        {
            SuspectAction.OrderSurrender => OrderRange,
            SuspectAction.Tackle => TackleRange,
            SuspectAction.Restrain => RestrainRange,
            SuspectAction.Search => SearchRange,
            SuspectAction.Shoot => ShootRange,
            _ => 5f,
        };

        private static ActionResult Ok(ActionResult r, string msg) { r.Ok = true; r.Message = msg; return r; }
        private static ActionResult Fail(ActionResult r, string msg) { r.Ok = false; r.Message = msg; return r; }
    }
}
#endif
