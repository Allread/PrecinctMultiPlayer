#if IL2CPP
using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrecinctMP
{
    /// <summary>
    /// Accès aux classes du jeu (trouvées dans Assembly-CSharp.dll) :
    ///   Il2Cpp.PlayerManager      : GetPlayerGameObject, IsInVehicle, GetPlayerVehicleController
    ///   Il2Cpp.VehicleController  : GetRigidbody, GetVehicleType, GetCurrentlyAppliedLivery, GetCurrentSiren
    ///   Il2Cpp.VehicleManager     : GetVehicleControllerPrefabFromType(type, livery)
    ///   Il2Cpp.Siren              : GetState() -> Off / LightsAndAudio / LightsOnly... / Blip
    /// Tout est protégé : si une mise à jour du jeu change une méthode, on retombe
    /// sur les heuristiques au lieu de planter.
    /// </summary>
    public static class GameApi
    {
        private static PlayerManager _player;
        private static VehicleManager _vehicles;
        private static float _nextLookup;
        private static bool _warned;

        public static bool Available => Find() && _player != null;

        private static bool Find()
        {
            if (_player != null && _vehicles != null) return true;
            if (Time.unscaledTime < _nextLookup) return _player != null;
            _nextLookup = Time.unscaledTime + 2f;
            try
            {
                if (_player == null) _player = Object.FindObjectOfType<PlayerManager>();
                if (_vehicles == null) _vehicles = Object.FindObjectOfType<VehicleManager>();
            }
            catch (Exception e) { WarnOnce("recherche des managers", e); }
            return _player != null;
        }

        public static GameObject PlayerObject()
        {
            if (!Find()) return null;
            try { return _player.GetPlayerGameObject(); }
            catch (Exception e) { WarnOnce("GetPlayerGameObject", e); return null; }
        }

        /// <summary>Personnage du joueur local (sert d'« auteur » des actions des clients chez l'hôte).</summary>
        public static Il2CppFallenTreeGames.CharacterController.Character PlayerCharacter()
        {
            if (!Find()) return null;
            try { return _player.GetPlayerCharacter(); }
            catch (Exception e) { WarnOnce("GetPlayerCharacter", e); return null; }
        }

        public static VehicleController PlayerVehicle()
        {
            if (!Find()) return null;
            try { return _player.IsInVehicle() ? _player.GetPlayerVehicleController() : null; }
            catch (Exception e) { WarnOnce("GetPlayerVehicleController", e); return null; }
        }

        public static void ReadVehicle(VehicleController v, ref PlayerState s)
        {
            try
            {
                var rb = v.GetRigidbody();
                var t = rb != null ? rb.transform : v.transform;
                s.VehPos = t.position;
                s.VehRot = t.rotation;
                s.VehVel = rb != null ? rb.velocity : Vector3.zero;
                s.VehicleType = (int)v.GetVehicleType();
                s.Livery = (int)v.GetCurrentlyAppliedLivery();
                var siren = v.GetCurrentSiren();
                s.Siren = siren != null ? (byte)siren.GetState() : (byte)0;
            }
            catch (Exception e) { WarnOnce("lecture du véhicule", e); }
        }

        /// <summary>Prefab d'origine d'une voiture (modèle + livrée), ou null.</summary>
        public static GameObject VehiclePrefab(int type, int livery)
        {
            if (!Find() || _vehicles == null) return null;
            try
            {
                var vc = _vehicles.GetVehicleControllerPrefabFromType(
                    (VehicleManager.VehicleType)type,
                    (CustomisationManager.VehicleLiveryCustomisation)livery);
                // Repli : livrée « Undefined » (0) puis « Default » (1), utile pour les voitures civiles
                for (int l = 0; vc == null && l <= 1; l++)
                    if (l != livery)
                        vc = _vehicles.GetVehicleControllerPrefabFromType(
                            (VehicleManager.VehicleType)type, (CustomisationManager.VehicleLiveryCustomisation)l);
                return vc != null ? vc.gameObject : null;
            }
            catch (Exception e) { WarnOnce("GetVehicleControllerPrefabFromType", e); return null; }
        }

        private static bool _inputBlockedByUs;

        /// <summary>
        /// Bloque/débloque les commandes du joueur (PlayerInput.SetPlayerInputDisabled).
        /// On ne débloque que si c'est nous qui avons bloqué, pour ne pas casser
        /// une cinématique ou un menu du jeu.
        /// </summary>
        public static void SetInputBlocked(bool blocked)
        {
            if (!Find()) return;
            try
            {
                var input = _player.GetPlayerInput();
                if (input == null) return;
                if (blocked)
                {
                    if (!input.IsPlayerInputDisabled())
                    {
                        input.SetPlayerInputDisabled(true);
                        _inputBlockedByUs = true;
                    }
                }
                else if (_inputBlockedByUs)
                {
                    input.SetPlayerInputDisabled(false);
                    _inputBlockedByUs = false;
                }
            }
            catch (Exception e) { WarnOnce("SetPlayerInputDisabled", e); }
        }

        public static string VehicleTypeName(int type) => ((VehicleManager.VehicleType)type).ToString();

        private static void WarnOnce(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            MelonLogger.Warning($"[MP] API du jeu indisponible ({what}) : {e.Message}. Repli sur la détection générique.");
        }
    }
}
#endif
