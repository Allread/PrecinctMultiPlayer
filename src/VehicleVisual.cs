using UnityEngine;

namespace PrecinctMP
{
    /// <summary>Voiture d'un joueur distant : copie visuelle du prefab du jeu (même modèle, même livrée).</summary>
    public static class VehicleVisual
    {
        public static GameObject Create(int vehicleType, int livery, string name)
        {
#if IL2CPP
            return VisualClone.Create(GameApi.VehiclePrefab(vehicleType, livery), name, keepAnimator: false);
#else
            return null;
#endif
        }
    }
}
