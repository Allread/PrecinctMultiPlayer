#if IL2CPP
using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrecinctMP
{
    /// <summary>
    /// Client connecté : coupe la génération de crimes et d'appels locaux, pour
    /// ne voir que ceux de l'hôte. Patchs Harmony posés une fois au démarrage,
    /// actifs uniquement quand <see cref="Active"/> est vrai.
    /// Chaque patch est posé séparément : si une mise à jour du jeu renomme une
    /// méthode, les autres continuent de fonctionner.
    /// </summary>
    public static class CrimeSuppression
    {
        public static bool Active { get; private set; }
        private static int _installed;

        private static readonly string[] SkipMethods =
        {
            nameof(AmbientCrimeManager.CheckAndTriggerNewAmbientCrimeIfAppropriate),
            nameof(AmbientCrimeManager.CheckNewDispatchCallouts),
            nameof(AmbientCrimeManager.CheckNewAmbientCallouts),
        };

        private static readonly string[] FalseMethods =
        {
            nameof(CalloutManager.CanTriggerDispatchCallouts),
            nameof(CalloutManager.CanTriggerNonStoryCallouts),
            nameof(CalloutManager.CanTriggerTrafficCollisionCrimes),
            nameof(CalloutManager.CanTriggerDynamicAmbientCrime),
        };

        public static void Install(HarmonyLib.Harmony harmony)
        {
            var skip = new HarmonyMethod(typeof(CrimeSuppression).GetMethod(nameof(SkipPrefix)));
            var no = new HarmonyMethod(typeof(CrimeSuppression).GetMethod(nameof(FalsePostfix)));

            foreach (var m in SkipMethods) Patch(harmony, typeof(AmbientCrimeManager), m, prefix: skip);
            foreach (var m in FalseMethods) Patch(harmony, typeof(CalloutManager), m, postfix: no);

            MelonLogger.Msg($"[MP] Blocage des crimes client : {_installed}/{SkipMethods.Length + FalseMethods.Length} patchs posés.");
        }

        private static void Patch(HarmonyLib.Harmony h, Type type, string method, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            try
            {
                var target = AccessTools.Method(type, method);
                if (target == null) { MelonLogger.Warning($"[MP] Méthode introuvable : {type.Name}.{method}"); return; }
                h.Patch(target, prefix, postfix);
                _installed++;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[MP] Patch {type.Name}.{method} impossible : {e.Message}");
            }
        }

        // Harmony : renvoyer false dans un prefix = ne pas exécuter la méthode du jeu
        public static bool SkipPrefix() => !Active;

        public static void FalsePostfix(ref bool __result)
        {
            if (Active) __result = false;
        }

        /// <summary>Active/désactive le blocage. À l'activation, efface les crimes locaux déjà en cours.</summary>
        public static void SetActive(bool active)
        {
            if (Active == active) return;
            Active = active;
            if (!active) { MelonLogger.Msg("[MP] Crimes locaux réactivés."); return; }

            try
            {
                var mgr = Object.FindObjectOfType<AmbientCrimeManager>();
                mgr?.CancelAndClearAmbientCrimes();
                MelonLogger.Msg("[MP] Crimes locaux coupés : seuls ceux de l'hôte comptent.");
            }
            catch (Exception e) { MelonLogger.Warning("[MP] Nettoyage des crimes locaux impossible : " + e.Message); }
        }
    }
}
#endif
