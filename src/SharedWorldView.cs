using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if IL2CPP
using Il2CppFallenTreeGames.CharacterController;
#endif

namespace PrecinctMP
{
    /// <summary>
    /// Client : affiche les crimes et les suspects de l'hôte.
    ///  - Appels : liste dans le HUD + marqueur au-dessus du lieu.
    ///  - Suspects : copie animée d'un personnage du jeu avec la même apparence
    ///    (repli : un passant local quelconque, puis une capsule rouge),
    ///    ou la voiture qu'ils conduisent.
    /// Les suspects non mis à jour depuis 1,5 s sont retirés.
    /// </summary>
    public class SharedWorldView
    {
        // ---------- Appels ----------

        private List<IncidentInfo> _incidents = new List<IncidentInfo>();
        private float _incidentsTime;

        public int IncidentCount => _incidents.Count;

        public void OnIncidents(List<IncidentInfo> list)
        {
            _incidents = list;
            _incidentsTime = Time.time;
        }

        // ---------- Suspects ----------

        private class Suspect
        {
            public ushort Id;
            public SuspectState Last;
            public float LastTime;
            public GameObject Capsule;          // repli + "racine" de référence
            public GameObject Model;
            public AnimatorSync ModelAnim;
            public Vector3 ModelOffsetPos;
            public Quaternion ModelOffsetRot = Quaternion.identity;
            public string ModelName;
            public float NextModelTry;
            public GameObject Car;
            public int CarType = -1;
        }

        private readonly Dictionary<ushort, Suspect> _suspects = new Dictionary<ushort, Suspect>();
        private const float SuspectTimeout = 1.5f;

        public int SuspectCount => _suspects.Count;

        public void OnSuspects(List<SuspectState> list)
        {
            foreach (var s in list)
            {
                if (!_suspects.TryGetValue(s.Id, out var v))
                {
                    v = new Suspect { Id = s.Id };
                    v.Capsule = Ghost(PrimitiveType.Capsule, $"MP_Suspect_{s.Id}", new Vector3(0.6f, 0.9f, 0.6f), new Color(0.9f, 0.2f, 0.2f));
                    v.Capsule.transform.SetPositionAndRotation(s.Pos + Vector3.up * 0.9f, s.Rot);
                    _suspects[s.Id] = v;
                }
                if (s.InVehicle && s.VehicleType != v.CarType) RebuildCar(v, s);
                v.Last = s;
                v.LastTime = Time.time;
            }
        }

        public void Update()
        {
            float dt = Time.deltaTime, now = Time.time;
            float k = 1f - Mathf.Exp(-12f * dt);

            List<ushort> gone = null;
            foreach (var v in _suspects.Values)
            {
                if (now - v.LastTime > SuspectTimeout) { (gone ??= new List<ushort>()).Add(v.Id); continue; }
                var s = v.Last;

                if (!s.InVehicle && (v.Model == null || v.ModelName != s.Model)) TryCreateModel(v, s.Model);

                bool showCar = s.InVehicle && v.Car != null;
                if (v.Car != null) v.Car.SetActive(showCar);
                if (v.Model != null) { v.Model.SetActive(!s.InVehicle); v.Capsule.SetActive(false); }
                else v.Capsule.SetActive(!s.InVehicle);

                if (s.InVehicle)
                {
                    if (v.Car == null) continue;
                    var t = v.Car.transform;
                    Vector3 target = s.VehPos + s.VehVel * Mathf.Min(now - v.LastTime, 0.25f);
                    if ((t.position - target).sqrMagnitude > 30f * 30f) t.SetPositionAndRotation(target, s.VehRot);
                    t.position = Vector3.Lerp(t.position, target, k);
                    t.rotation = Quaternion.Slerp(t.rotation, s.VehRot, k);
                    Place(v, t.position, t.rotation);
                }
                else
                {
                    var c = v.Capsule.transform;
                    Vector3 root = c.position - Vector3.up * 0.9f;
                    Quaternion rot = c.rotation;
                    if ((root - s.Pos).sqrMagnitude > 15f * 15f) { root = s.Pos; rot = s.Rot; }
                    Place(v, Vector3.Lerp(root, s.Pos, k), Quaternion.Slerp(rot, s.Rot, k));

                    if (v.ModelAnim != null && v.ModelAnim.ParamCount == 0 && s.Anim != null)
                        v.ModelAnim = new AnimatorSync(v.ModelAnim.Animator);
                    if (v.ModelAnim != null)
                    {
                        try
                        {
                            v.ModelAnim.Apply(s.Anim, s.AnimParamCount, dt);
                            v.ModelAnim.ApplyStates(s.AnimStates, now);
                        }
                        catch { v.ModelAnim = null; }
                    }
                }
            }
            if (gone != null) foreach (var id in gone) Remove(id);
        }

        public void Clear()
        {
            foreach (var id in new List<ushort>(_suspects.Keys)) Remove(id);
            _incidents = new List<IncidentInfo>();
        }

        private void Remove(ushort id)
        {
            if (!_suspects.TryGetValue(id, out var v)) return;
            Object.Destroy(v.Capsule);
            if (v.Model != null) Object.Destroy(v.Model);
            if (v.Car != null) Object.Destroy(v.Car);
            _suspects.Remove(id);
        }

        private static void Place(Suspect v, Vector3 root, Quaternion rot)
        {
            v.Capsule.transform.SetPositionAndRotation(root + Vector3.up * 0.9f, rot);
            if (v.Model != null)
                v.Model.transform.SetPositionAndRotation(root + rot * v.ModelOffsetPos, rot * v.ModelOffsetRot);
        }

        // ---------- Apparence des suspects ----------

        // Modèle de personnage local à copier, par nom d'apparence (cache)
        private readonly Dictionary<string, GameObject> _templates = new Dictionary<string, GameObject>();
        private float _nextTemplateScan;

        private void TryCreateModel(Suspect v, string modelName)
        {
            if (Time.time < v.NextModelTry) return;
            v.NextModelTry = Time.time + 3f;

            var template = FindTemplate(modelName ?? "");
            if (template == null) return;
            var anim = AnimatorSync.FindBodyAnimator(template);
            if (anim == null) return;

            var root = template.transform;
            var at = anim.transform;
            var offPos = Quaternion.Inverse(root.rotation) * (at.position - root.position);
            var offRot = Quaternion.Inverse(root.rotation) * at.rotation;

            var model = VisualClone.Create(anim.gameObject, $"MP_SuspectModel_{v.Id}", keepAnimator: true);
            if (model == null) return;
            var a = model.GetComponent<Animator>();
            if (a == null) { Object.Destroy(model); return; }
            a.applyRootMotion = false;
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (v.Model != null) Object.Destroy(v.Model);
            v.Model = model;
            v.ModelAnim = new AnimatorSync(a);
            v.ModelOffsetPos = offPos;
            v.ModelOffsetRot = offRot;
            // Si l'apparence exacte n'existe pas localement, on garde un passant quelconque
            // et on ne retente plus pour ce nom.
            v.ModelName = modelName;
        }

        /// <summary>
        /// Cherche un personnage local (PNJ) avec la même apparence ; sinon n'importe
        /// quel PNJ (apparence approximative). Le joueur local est exclu.
        /// </summary>
        private GameObject FindTemplate(string modelName)
        {
            if (_templates.TryGetValue(modelName, out var cached) && cached != null) return cached;
#if IL2CPP
            if (Time.time < _nextTemplateScan) return _templates.TryGetValue("", out var any) && any != null ? any : null;
            _nextTemplateScan = Time.time + 2f;

            GameObject fallback = null;
            var player = GameApi.PlayerObject();
            try
            {
                foreach (var ch in Object.FindObjectsOfType<Character>())
                {
                    if (ch == null) continue;
                    var go = ch.gameObject;
                    if (player != null && go == player) continue;
                    string name = HostCrimes.ModelName(go);
                    if (name.Length > 0 && !_templates.ContainsKey(name)) _templates[name] = go;
                    if (fallback == null) fallback = go;
                }
            }
            catch { /* scène en cours de chargement */ }

            if (fallback != null) _templates[""] = fallback;
            if (_templates.TryGetValue(modelName, out cached) && cached != null) return cached;
            return fallback;
#else
            return null;
#endif
        }

        private static void RebuildCar(Suspect v, SuspectState s)
        {
            v.CarType = s.VehicleType;
            var car = s.VehicleType > 0 ? VehicleVisual.Create(s.VehicleType, 0, $"MP_SuspectCar_{v.Id}") : null;
            if (car == null) car = Ghost(PrimitiveType.Cube, $"MP_SuspectCar_{v.Id}", new Vector3(1.9f, 1.4f, 4.7f), Color.gray);
            car.transform.SetPositionAndRotation(s.VehPos, s.VehRot);
            if (v.Car != null) Object.Destroy(v.Car);
            v.Car = car;
        }

        // ---------- HUD ----------

        public void DrawHud(float x, float y)
        {
            var cam = Camera.main;
            if (cam == null || _incidents.Count == 0) return;
            if (Time.time - _incidentsTime > 5f) return; // plus de nouvelles de l'hôte

            // Marqueurs au-dessus des lieux
            foreach (var c in _incidents)
            {
                Vector3 sp = cam.WorldToScreenPoint(c.Pos + Vector3.up * 4f);
                if (sp.z <= 0f) continue;
                float d = Vector3.Distance(cam.transform.position, c.Pos);
                string who = c.HostOnIt ? " [HÔTE]" : "";
                GUI.Label(new Rect(sp.x - 110f, Screen.height - sp.y - 12f, 220f, 24f), $"▼ {CrimeName(c.CrimeType)} ({d:0} m){who}");
            }

            // Liste des appels, du plus proche au plus loin
            var sorted = new List<IncidentInfo>(_incidents);
            Vector3 me = cam.transform.position;
            sorted.Sort((a, b) => (a.Pos - me).sqrMagnitude.CompareTo((b.Pos - me).sqrMagnitude));
            int lines = Mathf.Min(sorted.Count, 6);
            GUI.Box(new Rect(x, y, 330, 26 + lines * 18), "APPELS EN COURS (hôte)");
            for (int i = 0; i < lines; i++)
            {
                var c = sorted[i];
                var sb = new StringBuilder();
                sb.Append(CrimeName(c.CrimeType)).Append(" — ").Append(Vector3.Distance(me, c.Pos).ToString("0")).Append(" m");
                if (c.Suspects > 0) sb.Append(" — ").Append(c.Resolved).Append('/').Append(c.Suspects).Append(" suspects");
                if (c.HostOnIt) sb.Append(" [HÔTE]");
                GUI.Label(new Rect(x + 10, y + 22 + i * 18, 320, 20), sb.ToString());
            }
        }

        /// <summary>Pseudos/statuts au-dessus des suspects.</summary>
        public void DrawSuspectLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var v in _suspects.Values)
            {
                Vector3 p = v.Last.InVehicle && v.Car != null ? v.Car.transform.position : v.Capsule.transform.position;
                Vector3 sp = cam.WorldToScreenPoint(p + Vector3.up * 1.6f);
                if (sp.z <= 0f || sp.z > 120f) continue;
                GUI.Label(new Rect(sp.x - 70f, Screen.height - sp.y - 12f, 140f, 24f), "Suspect " + StatusName(v.Last.Status));
            }
        }

        public static string StatusName(byte s) => s switch
        {
            0 => "",
            1 => "(se rend)",
            2 => "(arrêté)",
            3 => "(neutralisé)",
            4 => "(enfui)",
            5 => "(menotté)",
            _ => "",
        };

        /// <summary>« VehicleTheft » -> « Vehicle Theft » (nom de l'enum du jeu).</summary>
        public static string CrimeName(short type)
        {
#if IL2CPP
            string raw = ((Il2Cpp.AmbientCrimeManager.AmbientCrimeType)type).ToString();
#else
            string raw = "Crime " + type;
#endif
            var sb = new StringBuilder(raw.Length + 8);
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (ch == '_') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(ch) && char.IsLower(raw[i - 1])) sb.Append(' ');
                sb.Append(ch);
            }
            return sb.ToString();
        }

        private static GameObject Ghost(PrimitiveType type, string name, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material.color = color;
            Object.DontDestroyOnLoad(go);
            return go;
        }
    }
}
