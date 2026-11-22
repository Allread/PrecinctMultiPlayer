using System.Collections.Generic;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// Représentation des autres joueurs.
    ///  - À pied : copie du modèle du policier local, animée avec les paramètres
    ///    Animator reçus du réseau (repli : capsule bleue).
    ///  - En voiture : copie du vrai modèle de voiture avec la bonne livrée
    ///    (repli : boîte blanche).
    /// Aucune collision pour l'instant.
    /// </summary>
    public class RemotePlayers
    {
        private class Remote
        {
            public byte Id;
            public string Name;
            public GameObject Capsule;          // repli
            public GameObject Model;            // policier animé
            public AnimatorSync ModelAnim;
            public GameObject Car;
            public PlayerState Last;
            public float LastTime;
            public bool HasState;
            public int CarType = -1, CarLivery = -1;
            public float NextModelTry;

            public Transform BodyRoot => Model != null ? Model.transform : Capsule.transform;
        }

        private readonly Dictionary<byte, Remote> _remotes = new Dictionary<byte, Remote>();

        // Modèle de référence : le policier local
        private Transform _templateRoot;
        private Animator _templateAnim;
        private Vector3 _modelOffsetPos;      // position de l'Animator par rapport à la racine du joueur
        private Quaternion _modelOffsetRot = Quaternion.identity;

        public int Count => _remotes.Count;

        /// <summary>À appeler à chaque image avec le joueur local et son Animator.</summary>
        public void SetTemplate(Transform playerRoot, Animator bodyAnimator)
        {
            _templateRoot = playerRoot;
            _templateAnim = bodyAnimator;
        }

        public void Add(byte id, string name)
        {
            if (_remotes.ContainsKey(id)) return;
            var r = new Remote { Id = id, Name = name };
            r.Capsule = MakeGhost(PrimitiveType.Capsule, $"MP_Player_{id}", new Vector3(0.6f, 0.9f, 0.6f), new Color(0.2f, 0.4f, 1f));
            r.Car = MakeGhost(PrimitiveType.Cube, $"MP_Car_{id}", new Vector3(1.9f, 1.4f, 4.7f), Color.white);
            r.Capsule.SetActive(false);
            r.Car.SetActive(false);
            _remotes[id] = r;
        }

        public void Remove(byte id)
        {
            if (!_remotes.TryGetValue(id, out var r)) return;
            Object.Destroy(r.Capsule);
            if (r.Model != null) Object.Destroy(r.Model);
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
                PlaceBody(r, s.Pos, s.Rot);
                if (s.InVehicle) r.Car.transform.SetPositionAndRotation(s.VehPos, s.VehRot);
                r.HasState = true;
            }
        }

        public void Update()
        {
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-15f * dt); // lissage indépendant du framerate
            foreach (var r in _remotes.Values)
            {
                if (!r.HasState) continue;
                var s = r.Last;
                float age = Mathf.Min(Time.time - r.LastTime, 0.25f);

                if (r.Model == null) TryCreateModel(r);

                r.Car.SetActive(s.InVehicle);
                if (r.Model != null) { r.Model.SetActive(!s.InVehicle); r.Capsule.SetActive(false); }
                else r.Capsule.SetActive(!s.InVehicle);

                if (s.InVehicle)
                {
                    Vector3 target = s.VehPos + s.VehVel * age; // extrapolation courte
                    var t = r.Car.transform;
                    if ((t.position - target).sqrMagnitude > 30f * 30f) t.SetPositionAndRotation(target, s.VehRot);
                    t.position = Vector3.Lerp(t.position, target, k);
                    t.rotation = Quaternion.Slerp(t.rotation, s.VehRot, k);
                    PlaceBody(r, t.position, t.rotation); // le corps suit la voiture (caché)
                }
                else
                {
                    // Position/rotation actuelles de la "racine joueur" reconstituée
                    GetBody(r, out var pos, out var rot);
                    if ((pos - s.Pos).sqrMagnitude > 15f * 15f) { pos = s.Pos; rot = s.Rot; } // téléportation
                    PlaceBody(r, Vector3.Lerp(pos, s.Pos, k), Quaternion.Slerp(rot, s.Rot, k));
                    // L'Animator peut n'exposer ses paramètres qu'après sa 1re image : on relit si besoin
                    if (r.ModelAnim != null && r.ModelAnim.ParamCount == 0 && s.Anim != null)
                        r.ModelAnim = new AnimatorSync(r.ModelAnim.Animator);
                    if (r.ModelAnim != null)
                    {
                        try { r.ModelAnim.Apply(s.Anim, s.AnimParamCount, dt); }
                        catch { r.ModelAnim = null; }
                    }
                }
            }
        }

        // ---------- Modèle du policier ----------

        private void TryCreateModel(Remote r)
        {
            if (_templateAnim == null || _templateRoot == null || Time.time < r.NextModelTry) return;
            r.NextModelTry = Time.time + 5f;

            var animT = _templateAnim.transform;
            _modelOffsetPos = Quaternion.Inverse(_templateRoot.rotation) * (animT.position - _templateRoot.position);
            _modelOffsetRot = Quaternion.Inverse(_templateRoot.rotation) * animT.rotation;

            var model = VisualClone.Create(_templateAnim.gameObject, $"MP_Cop_{r.Id}", keepAnimator: true);
            if (model == null) return;

            var anim = model.GetComponent<Animator>();
            if (anim == null) { Object.Destroy(model); return; }
            anim.applyRootMotion = false;                        // c'est le réseau qui déplace le modèle
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Reprend la position actuelle de la capsule
            var capT = r.Capsule.transform;
            Vector3 rootPos = capT.position - Vector3.up * 0.9f;
            r.Model = model;
            r.ModelAnim = new AnimatorSync(anim);
            PlaceBody(r, rootPos, capT.rotation);
            r.Model.SetActive(!r.Last.InVehicle);
            r.Capsule.SetActive(false);
        }

        /// <summary>Place le corps à partir de la position/rotation de la racine du joueur distant.</summary>
        private void PlaceBody(Remote r, Vector3 rootPos, Quaternion rootRot)
        {
            if (r.Model != null)
                r.Model.transform.SetPositionAndRotation(rootPos + rootRot * _modelOffsetPos, rootRot * _modelOffsetRot);
            r.Capsule.transform.SetPositionAndRotation(rootPos + Vector3.up * 0.9f, rootRot);
        }

        /// <summary>Inverse de PlaceBody : retrouve la racine du joueur à partir du corps affiché.</summary>
        private void GetBody(Remote r, out Vector3 rootPos, out Quaternion rootRot)
        {
            var c = r.Capsule.transform;
            rootPos = c.position - Vector3.up * 0.9f;
            rootRot = c.rotation;
        }

        // ---------- Voiture ----------

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

        // ---------- Affichage ----------

        /// <summary>Pseudos au-dessus des joueurs distants.</summary>
        public void DrawLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var r in _remotes.Values)
            {
                if (!r.HasState) continue;
                GetBody(r, out var root, out _);
                var anchor = r.Last.InVehicle ? r.Car.transform.position : root;
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
