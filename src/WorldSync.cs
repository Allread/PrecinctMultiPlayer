using System;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrecinctMP
{
    /// <summary>Heure et météo de l'hôte.</summary>
    public struct WorldState
    {
        public float Hours;        // 0..24
        public byte Weather;       // Il2CppTimeOfDay.WeatherType : normal, lightRain, heavyRain, storm, foggy
        public float SpeedMult;    // vitesse d'écoulement du temps

        public void Write(NetDataWriter w)
        {
            w.Put((byte)MsgType.World);
            w.Put(Hours);
            w.Put(Weather);
            w.Put(SpeedMult);
        }

        public static WorldState Read(NetDataReader r) => new WorldState
        {
            Hours = r.GetFloat(),
            Weather = r.GetByte(),
            SpeedMult = r.GetFloat(),
        };
    }

    /// <summary>
    /// Étape 1 du monde partagé : l'hôte impose son heure et sa météo.
    ///  - Hôte : lit TimeOfDayManager et l'envoie toutes les 2 s.
    ///  - Client : coupe sa météo aléatoire (SetStaticWeather), recale l'heure
    ///    si elle dérive de plus de 10 min de jeu, et suit les changements de
    ///    météo de l'hôte (StartWeatherChange, avec la transition du jeu).
    /// À la déconnexion, le client retrouve sa météo normale.
    /// </summary>
    public class WorldSync
    {
        public const float SendInterval = 2f;
        private const float MaxDriftHours = 10f / 60f;

        private float _nextSend;
        private bool _clientLocked;
        private int _appliedWeather = -1;
        private float _appliedSpeed = -1f;
        private float _origSpeed = 1f;

#if IL2CPP
        private Il2Cpp.TimeOfDayManager _tod;
        private float _nextLookup;
        private bool _warned;

        private Il2Cpp.TimeOfDayManager Tod()
        {
            if (_tod != null) return _tod;
            if (Time.unscaledTime < _nextLookup) return null;
            _nextLookup = Time.unscaledTime + 2f;
            try { _tod = Object.FindObjectOfType<Il2Cpp.TimeOfDayManager>(); }
            catch (Exception e) { Warn("recherche TimeOfDayManager", e); }
            return _tod;
        }
#endif

        public string Status { get; private set; } = "";

        /// <summary>Hôte : renvoie vrai quand il faut envoyer l'état du monde.</summary>
        public bool TryCaptureForSend(out WorldState w)
        {
            w = default;
            if (Time.unscaledTime < _nextSend) return false;
            _nextSend = Time.unscaledTime + SendInterval;
#if IL2CPP
            var tod = Tod();
            if (tod == null) return false;
            try
            {
                w.Hours = tod.GetTimeInHours();
                w.Weather = (byte)tod.GetPerceivedWeatherType();
                w.SpeedMult = tod.GetTimeOfDaySpeedMultiplier();
                Status = $"monde envoyé : {Clock(w.Hours)}, {WeatherName(w.Weather)}";
                return true;
            }
            catch (Exception e) { Warn("lecture heure/météo", e); }
#endif
            return false;
        }

        /// <summary>Envoi immédiat au prochain tour (ex. : un joueur vient d'arriver).</summary>
        public void ForceSendSoon() => _nextSend = 0f;

        /// <summary>Client : applique l'état reçu de l'hôte.</summary>
        public void Apply(WorldState w)
        {
#if IL2CPP
            var tod = Tod();
            if (tod == null) return;
            try
            {
                if (!_clientLocked)
                {
                    _origSpeed = tod.GetTimeOfDaySpeedMultiplier();
                    tod.SetStaticWeather(true); // plus de météo aléatoire chez le client
                    _clientLocked = true;
                    MelonLogger.Msg("[MP] Heure et météo synchronisées sur l'hôte.");
                }

                if (Mathf.Abs(w.SpeedMult - _appliedSpeed) > 0.001f)
                {
                    tod.SetTimeOfDaySpeedMultiplier(w.SpeedMult);
                    _appliedSpeed = w.SpeedMult;
                }

                float local = tod.GetTimeInHours();
                float diff = Mathf.Abs(local - w.Hours);
                diff = Mathf.Min(diff, 24f - diff); // passage de minuit
                if (diff > MaxDriftHours) tod.SetTimeInHours(w.Hours);

                if (w.Weather != _appliedWeather)
                {
                    if ((byte)tod.GetPerceivedWeatherType() != w.Weather)
                        tod.StartWeatherChange((Il2CppTimeOfDay.WeatherType)w.Weather, false);
                    _appliedWeather = w.Weather;
                }

                Status = $"monde de l'hôte : {Clock(w.Hours)}, {WeatherName(w.Weather)}";
            }
            catch (Exception e) { Warn("application heure/météo", e); }
#endif
        }

        /// <summary>Fin de session : le client retrouve sa météo et sa vitesse normales.</summary>
        public void Reset()
        {
#if IL2CPP
            if (_clientLocked && _tod != null)
            {
                try
                {
                    _tod.SetStaticWeather(false);
                    _tod.SetTimeOfDaySpeedMultiplier(_origSpeed);
                }
                catch (Exception e) { Warn("restauration météo", e); }
            }
#endif
            _clientLocked = false;
            _appliedWeather = -1;
            _appliedSpeed = -1f;
            _nextSend = 0f;
            Status = "";
        }

        public static string Clock(float hours)
        {
            int h = Mathf.FloorToInt(hours) % 24;
            int m = Mathf.FloorToInt((hours - Mathf.Floor(hours)) * 60f);
            return $"{h:00}h{m:00}";
        }

        public static string WeatherName(int w) => w switch
        {
            0 => "beau temps",
            1 => "pluie légère",
            2 => "forte pluie",
            3 => "orage",
            4 => "brouillard",
            _ => "météo " + w,
        };

#if IL2CPP
        private void Warn(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            MelonLogger.Warning($"[MP] Synchro heure/météo indisponible ({what}) : {e.Message}");
        }
#endif
    }
}
