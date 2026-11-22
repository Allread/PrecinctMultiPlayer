using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrecinctMP
{
    /// <summary>
    /// F7 : écrit dans UserData/PrecinctMP_dump.txt la hiérarchie autour du
    /// joueur et de la caméra, avec les composants. C'est ce fichier qu'il faut
    /// m'envoyer pour qu'on remplace les heuristiques par les vraies classes du jeu.
    /// </summary>
    public static class Diagnostics
    {
        public static void Dump(LocalPlayer local)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PrecinctMP dump ===");
            sb.AppendLine($"Unity {Application.unityVersion}");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                sb.AppendLine($"Scène {i}: {scene.name} ({scene.rootCount} objets racine)");
            }

            sb.AppendLine();
            sb.AppendLine("--- Objet tagué 'Player' ---");
            GameObject tagged = null;
            try { tagged = GameObject.FindWithTag("Player"); } catch (System.Exception) { }
            if (tagged != null) DumpUp(sb, tagged.transform); else sb.AppendLine("(aucun)");

            sb.AppendLine();
            sb.AppendLine("--- Caméra principale et ses parents ---");
            if (Camera.main != null) DumpUp(sb, Camera.main.transform); else sb.AppendLine("(aucune)");

            if (local.Player != null)
            {
                sb.AppendLine();
                sb.AppendLine("--- Joueur détecté : enfants (2 niveaux) ---");
                DumpDown(sb, local.Player, 0, 2);
            }

            sb.AppendLine();
            sb.AppendLine("--- Rigidbodies lourds à moins de 30 m de la caméra (voitures ?) ---");
            if (Camera.main != null)
            {
                var camPos = Camera.main.transform.position;
                foreach (var rb in Object.FindObjectsOfType<Rigidbody>())
                {
                    if (rb.mass < 300f) continue;
                    float d = Vector3.Distance(rb.position, camPos);
                    if (d > 30f) continue;
                    sb.AppendLine($"{FullPath(rb.transform)}  masse={rb.mass:0}  dist={d:0.0}");
                    sb.AppendLine("    " + Components(rb.gameObject));
                }
            }

            string file = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "PrecinctMP_dump.txt");
            File.WriteAllText(file, sb.ToString());
            MelonLogger.Msg($"[MP] Diagnostic écrit dans {file}");
        }

        private static void DumpUp(StringBuilder sb, Transform t)
        {
            for (int depth = 0; t != null; t = t.parent, depth++)
            {
                sb.AppendLine($"{new string(' ', depth * 2)}{t.name}  [{t.gameObject.tag}]");
                sb.AppendLine($"{new string(' ', depth * 2)}  {Components(t.gameObject)}");
            }
        }

        private static void DumpDown(StringBuilder sb, Transform t, int depth, int max)
        {
            sb.AppendLine($"{new string(' ', depth * 2)}{t.name}: {Components(t.gameObject)}");
            if (depth >= max) return;
            for (int i = 0; i < t.childCount; i++) DumpDown(sb, t.GetChild(i), depth + 1, max);
        }

        private static string Components(GameObject go)
        {
            var parts = new StringBuilder();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
#if IL2CPP
                parts.Append(c.GetIl2CppType().FullName).Append(", ");
#else
                parts.Append(c.GetType().FullName).Append(", ");
#endif
            }
            return parts.ToString().TrimEnd(',', ' ');
        }

        private static string FullPath(Transform t)
        {
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }
    }
}
