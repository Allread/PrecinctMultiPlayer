using System.Collections.Generic;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// Représentation des autres joueurs.
    /// Étape 1 : de simples formes (capsule = policier, boîte = voiture),
    /// sans collision, avec interpolation. Les vrais modèles viendront ensuite.
    /// </summary>
    public class RemotePlayers
    {
        private class Remote
        {
            public byte Id;
            public string Name;
            public GameObject Body;
            public GameObject Car;
            public PlayerState Last;
            public float LastTime;
            public bool HasState;
            public int CarType = -1, CarLivery = -1;
        }

        private readonly Dictionary<byte, Remote> _remotes = new Dictionary<byte, Remote>();

        public int Count => _remotes.Count;

        public void Add(byte id, string name)
        {
            if (_remotes.ContainsKey(id)) return;
            var r = new Remote { Id = id, Name = name };
            r.Body = MakeGhost(PrimitiveType.Capsule, $"MP_Player_{id}", new Vector3(0.6f, 0.9f, 0.6f), new Color(0.2f, 0.4f, 1f));
            r.Car = MakeGhost(PrimitiveType.Cube, $"MP_Car_{id}", new Vector3(1.9f, 1.4f, 4.7f), Color.white);
            r.Body.SetActive(false);
            r.Car.SetActive(false);
            _remotes[id] = r;
        }

        public void Remove(byte id)
        {
            if (!_remotes.TryGetValue(id, out var r)) return;
            Object.Destroy(r.Body);
            Object.Destroy(r.Car);
            _remotes.Remove(id);
        }

        public void Clear()
        {
            foreach (var id in new List<byte>(_remotes.Keys)) Remove(id);
        }

        public void OnState(PlayerState s)
        {
            if (!_remotes.TryGetValue(s.Id, out var r)) { Add(s.Id, "Joueur " + s.Id); r = _remotes[s.Id]; }
            if (r.HasState && s.Seq <= r.Last.Seq) return; // paquet en retard
            if (s.InVehicle && (s.VehicleType != r.CarType || s.Livery != r.CarLivery))
                RebuildCar(r, s);
            r.Last = s;
            r.LastTime = Time.time;
            if (!r.HasState)
            {
                // Premier état : on téléporte directement
                r.Body.transform.SetPositionAndRotation(s.Pos, s.Rot);
                if (s.InVehicle) r.Car.transform.SetPositionAndRotation(s.VehPos, s.VehRot);
                r.HasState = true;
            }
        }

        public void Update()
        {
            float k = 1f - Mathf.Exp(-15f * Time.deltaTime); // lissage indépendant du framerate
            foreach (var r in _remotes.Values)
            {
                if (!r.HasState) continue;
                var s = r.Last;
                float age = Mathf.Min(Time.time - r.LastTime, 0.25f);

                r.Car.SetActive(s.InVehicle);
                r.Body.SetActive(!s.InVehicle);

                if (s.InVehicle)
                {
                    Vector3 target = s.VehPos + s.VehVel * age; // extrapolation courte
                    var t = r.Car.transform;
                    t.position = Vector3.Lerp(t.position, target, k);
                    t.rotation = Quaternion.Slerp(t.rotation, s.VehRot, k);
                    r.Body.transform.position = t.position;
                }
                else
                {
                    var t = r.Body.transform;
                    t.position = Vector3.Lerp(t.position, s.Pos, k);
                    t.rotation = Quaternion.Slerp(t.rotation, s.Rot, k);
                }
            }
        }

        /// <summary>Remplace la boîte par le vrai modèle de voiture (même type et même livrée).</summary>
        private static void RebuildCar(Remote r, PlayerState s)
        {
            r.CarType = s.VehicleType;
            r.CarLivery = s.Livery;
            if (s.VehicleType <= 0) return; // type inconnu : on garde la boîte

            var model = VehicleVisual.Create(s.VehicleType, s.Livery, $"MP_Car_{r.Id}");
            if (model == null) return;

            Vector3 pos = r.Car.transform.position;
            Quaternion rot = r.Car.transform.rotation;
            bool active = r.Car.activeSelf;
            Object.Destroy(r.Car);
            r.Car = model;
            r.Car.transform.SetPositionAndRotation(pos, rot);
            r.Car.SetActive(active);
        }

        /// <summary>Pseudos au-dessus des joueurs distants.</summary>
        public void DrawLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var r in _remotes.Values)
            {
                if (!r.HasState) continue;
                var anchor = r.Last.InVehicle ? r.Car.transform.position : r.Body.transform.position;
                Vector3 sp = cam.WorldToScreenPoint(anchor + Vector3.up * 2.2f);
                if (sp.z <= 0f) continue;
                var rect = new Rect(sp.x - 80f, Screen.height - sp.y - 12f, 160f, 24f);
                string siren = r.Last.InVehicle && r.Last.Siren != 0 ? "  [GYRO]" : "";
                GUI.Label(rect, $"{r.Name}  ({Vector3.Distance(cam.transform.position, anchor):0} m){siren}");
            }
        }

        private static GameObject MakeGhost(PrimitiveType type, string name, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // pas de collision pour l'instant
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material.color = color;
            Object.DontDestroyOnLoad(go);
            return go;
        }
    }
}
