using System;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrecinctMP
{
    /// <summary>
    /// Copie « coquille vide » d'un objet du jeu : on garde uniquement l'aspect
    /// (meshes, et éventuellement l'Animator) et on supprime scripts, physique,
    /// sons, caméras et lumières, pour que le jeu ne la prenne pas pour un vrai
    /// personnage ou une vraie voiture (IA, pool, sauvegarde...).
    /// </summary>
    public static class VisualClone
    {
        private static GameObject _staging;

        public static GameObject Create(GameObject source, string name, bool keepAnimator)
        {
            if (source == null) return null;
            try
            {
                // Parent inactif : Awake/OnEnable des scripts copiés ne s'exécutent jamais.
                if (_staging == null)
                {
                    _staging = new GameObject("PrecinctMP_Staging");
                    _staging.SetActive(false);
                    Object.DontDestroyOnLoad(_staging);
                }

                var clone = Object.Instantiate(source, _staging.transform);
                clone.name = name;
                Strip(clone, keepAnimator);
                clone.transform.SetParent(null, false);
                Object.DontDestroyOnLoad(clone);
                return clone;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[MP] Copie de '{source.name}' impossible : {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Plusieurs passes car certains composants en exigent d'autres
        /// (RequireComponent) et ne peuvent être supprimés qu'après eux.
        /// </summary>
        private static void Strip(GameObject root, bool keepAnimator)
        {
            for (int pass = 0; pass < 5; pass++)
            {
                int removed = 0;
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || Keep(c, pass, keepAnimator)) continue;
                    try { Object.DestroyImmediate(c); removed++; } catch { /* dépendance : passe suivante */ }
                }
                if (removed == 0 && pass > 0) break;
            }
        }

        private static bool Keep(Component c, int pass, bool keepAnimator)
        {
            if (Is<Transform>(c) || Is<MeshFilter>(c) || Is<MeshRenderer>(c)
                || Is<SkinnedMeshRenderer>(c) || Is<LODGroup>(c)) return true;
            if (keepAnimator && Is<Animator>(c)) return true;
            // Rigidbody en dernier (joints, roues, ragdoll en dépendent)
            if (pass == 0 && (Is<Rigidbody>(c) || Is<Joint>(c))) return true;
            return false;
        }

#if IL2CPP
        private static bool Is<T>(Component c) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
            => c.TryCast<T>() != null;
#else
        private static bool Is<T>(Component c) where T : Component => c is T;
#endif
    }
}
