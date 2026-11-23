using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
#if IL2CPP
using Il2Cpp;
#endif

namespace PrecinctMP
{
    /// <summary>
    /// Client : actions sur les suspects de l'hôte.
    ///  - Touches (configurables) quand on est près d'un suspect : ordre de se rendre,
    ///    plaquer, menotter, fouiller. Une invite s'affiche à l'écran.
    ///  - Tirs : on intercepte CharacterGunController.Shoot du joueur local ; si la
    ///    trajectoire passe par un suspect, on demande à l'hôte d'appliquer les dégâts.
    /// L'hôte vérifie tout (distance, état du suspect) et renvoie un message.
    /// </summary>
    public class ClientActions
    {
        public const float OrderRange = 20f, CloseRange = 2.5f;
        private const float Cooldown = 0.6f;

        // Tirs du joueur local interceptés (patch Harmony), traités dans Update
        private static readonly List<(Vector3 origin, Vector3 target)> PendingShots = new List<(Vector3, Vector3)>();
        private static int _patched;

        private float _nextAction;
        private string _prompt = "";

        public static void Install(HarmonyLib.Harmony harmony)
        {
#if IL2CPP
            try
            {
                var target = HarmonyLib.AccessTools.Method(typeof(CharacterGunController), nameof(CharacterGunController.Shoot));
                var post = new HarmonyLib.HarmonyMethod(typeof(ClientActions).GetMethod(nameof(ShootPostfix)));
                if (target != null) { harmony.Patch(target, postfix: post); _patched++; }
                MelonLogger.Msg(_patched > 0 ? "[MP] Interception des tirs : OK." : "[MP] Interception des tirs : méthode Shoot introuvable.");
            }
            catch (Exception e) { MelonLogger.Warning("[MP] Interception des tirs impossible : " + e.Message); }
#endif
        }

#if IL2CPP
        /// <summary>Après chaque tir d'un personnage : on ne garde que ceux du joueur local.</summary>
        public static void ShootPostfix(CharacterGunController __instance, bool __result, Vector3 __0, GunEquippable __1)
        {
            if (!__result || !CrimeSuppression.Active) return; // uniquement client connecté
            try
            {
                var player = GameApi.PlayerObject();
                if (player == null || __instance == null || __instance.transform.root != player.transform.root) return;
                Vector3 origin = __1 != null ? __1.GetActualShootPosition() : __instance.transform.position + Vector3.up * 1.4f;
                if (PendingShots.Count < 16) PendingShots.Add((origin, __0));
            }
            catch { /* ne jamais casser le tir du jeu */ }
        }
#endif

        public string Prompt => _prompt;

        /// <summary>Appelé à chaque image côté client connecté.</summary>
        public void Update(NetSession session, SharedWorldView view, LocalPlayer local, KeyBindings keys, float shotDamage)
        {
            _prompt = "";
            if (session.IsHost || !session.IsConnected || local.Player == null) { PendingShots.Clear(); return; }

            // Tirs
            for (int i = 0; i < PendingShots.Count; i++)
            {
                var (origin, target) = PendingShots[i];
                if (view.RaycastSuspect(origin, target, out ushort id, out Vector3 hit))
                {
                    session.SendAction(new ActionRequest
                    {
                        Action = SuspectAction.Shoot,
                        SuspectId = id,
                        HitPos = hit,
                        Dir = (target - origin).normalized,
                        Damage = shotDamage,
                    });
                }
            }
            PendingShots.Clear();

            if (local.Vehicle != null) return; // actions de proximité : à pied uniquement

            Vector3 me = local.Player.position;
            if (!view.TryGetNearest(me, OrderRange, out ushort sid, out float dist, out byte status)) return;
            if (status == 2 || status == 3 || status == 4) return; // arrêté / mort / enfui

            bool close = dist <= CloseRange;
            bool restrained = status == 5;
            bool surrendering = status == 1;

            var parts = new List<string>();
            if (!restrained && !surrendering) parts.Add($"[{keys.Surrender}] Ordre de se rendre");
            if (close && !restrained) parts.Add($"[{keys.Tackle}] Plaquer");
            if (close && !restrained) parts.Add($"[{keys.Restrain}] Menotter");
            if (close && (restrained || surrendering)) parts.Add($"[{keys.Search}] Fouiller");
            _prompt = string.Join("    ", parts);

            if (Time.unscaledTime < _nextAction) return;
            SuspectAction? action = null;
            if (Hotkeys.Pressed(keys.SurrenderVk) && !restrained) action = SuspectAction.OrderSurrender;
            else if (Hotkeys.Pressed(keys.TackleVk) && close && !restrained) action = SuspectAction.Tackle;
            else if (Hotkeys.Pressed(keys.RestrainVk) && close && !restrained) action = SuspectAction.Restrain;
            else if (Hotkeys.Pressed(keys.SearchVk) && close) action = SuspectAction.Search;
            if (action == null) return;

            _nextAction = Time.unscaledTime + Cooldown;
            session.SendAction(new ActionRequest
            {
                Action = action.Value,
                SuspectId = sid,
                HitPos = me,
                Dir = local.Player.forward,
            });
        }

        public void DrawPrompt()
        {
            if (_prompt.Length == 0) return;
            float w = 560f;
            GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height - 120f, w, 30f), "");
            GUI.Label(new Rect((Screen.width - w) / 2f + 12f, Screen.height - 115f, w - 24f, 22f), "Suspect (hôte) : " + _prompt);
        }
    }

    /// <summary>Touches des actions, lues dans MelonPreferences (une lettre ou un chiffre).</summary>
    public class KeyBindings
    {
        public string Surrender = "Y", Tackle = "U", Restrain = "I", Search = "O";
        public int SurrenderVk => Vk(Surrender);
        public int TackleVk => Vk(Tackle);
        public int RestrainVk => Vk(Restrain);
        public int SearchVk => Vk(Search);

        /// <summary>Lettres A-Z et chiffres 0-9 : leur code touche Windows est leur code ASCII.</summary>
        private static int Vk(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            char c = char.ToUpperInvariant(key.Trim()[0]);
            return (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ? c : 0;
        }
    }
}
