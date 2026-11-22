using System;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrecinctMP
{
    /// <summary>
    /// Fabrique une copie « coquille vide » d'une voiture du jeu pour représenter
    /// la voiture d'un joueur distant : on garde uniquement les meshes et on
    /// supprime scripts, physique, sons et lumières, pour que le jeu ne la
    /// prenne pas pour une vraie voiture (IA, trafic, pool, sauvegarde...).
    /// </summary>
    public static class VehicleVisual
    {
        private static GameObject _staging;

        public static GameObject Create(int vehicleType, int livery, string name)
        {
#if IL2CPP
            try
            {
                var prefab = GameApi.VehiclePrefab(vehicleType, livery);
                if (prefab == null) return null;

                // Parent inactif : Awake/OnEnable des scripts du prefab ne s'exécutent pas.
                if (_staging == null)
                {
                    _staging = new GameObject("PrecinctMP_Staging");
                    _staging.SetActive(false);
                    Object.DontDestroyOnLoad(_staging);
                }

                var clone = Object.Instantiate(prefab, _staging.transform);
                clone.name = name;
                Strip(clone);
                clone.transform.SetParent(null, false);
                Object.DontDestroyOnLoad(clone);
                return clone;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[MP] Modèle de voiture {vehicleType} indisponible : {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>
        /// Plusieurs passes car certains composants en exigent d'autres
        /// (RequireComponent) et ne peuvent être supprimés qu'après eux.
        /// </summary>
        private static void Strip(GameObject root)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                int removed = 0;
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || Keep(c, pass)) continue;
                    try { Object.DestroyImmediate(c); removed++; } catch { /* dépendance : passe suivante */ }
                }
                if (removed == 0 && pass > 0) break;
            }
        }

        private static bool Keep(Component c, int pass)
        {
            if (Is<Transform>(c) || Is<MeshFilter>(c) || Is<MeshRenderer>(c)
                || Is<SkinnedMeshRenderer>(c) || Is<LODGroup>(c)) return true;
            // Rigidbody en dernier (les joints/roues en dépendent)
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
