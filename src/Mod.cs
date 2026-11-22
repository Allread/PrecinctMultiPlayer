using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(PrecinctMP.Mod), "PrecinctMP", "0.3.0", "Anthony")]
[assembly: MelonGame(null, null)] // nom exact du jeu/éditeur à renseigner une fois connu

namespace PrecinctMP
{
    /// <summary>
    /// Mod multijoueur expérimental pour The Precinct.
    /// v0.1 : connexion à deux, on se voit et on se déplace ensemble.
    /// v0.2 : vrai modèle animé du policier à pied, vraie voiture avec livrée.
    /// v0.2.1 : actions ponctuelles (menotter, tirer, sauter...) via l'état d'animation.
    /// v3.0.0 : monde partagé étape 1 — heure et météo de l'hôte imposées aux clients.
    /// v0.3.0 : étapes 2-3 — crimes/appels et suspects de l'hôte partagés, crimes du client coupés.
    ///
    /// F8  = débuter une session   F9 = rejoindre   F10 = quitter
    /// F7  = diagnostic  F11 = afficher/masquer le HUD
    /// </summary>
    public class Mod : MelonMod
    {
        private MelonPreferences_Entry<string> _name, _hostIp, _playerObject;
        private MelonPreferences_Entry<int> _port;
        private MelonPreferences_Entry<float> _minVehicleMass;

        private readonly NetSession _session = new NetSession();
        private readonly LocalPlayer _local = new LocalPlayer();
        private readonly RemotePlayers _remotes = new RemotePlayers();
        private readonly List<string> _feed = new List<string>();
        private readonly WorldSync _world = new WorldSync();
        private readonly SharedWorldView _sharedView = new SharedWorldView();
#if IL2CPP
        private readonly HostCrimes _hostCrimes = new HostCrimes();
#endif
        private readonly JoinDialog _joinDialog = new JoinDialog();
        private readonly HostDialog _hostDialog = new HostDialog();
        private bool AnyDialogOpen => _joinDialog.IsOpen || _hostDialog.IsOpen;

        private const float SendInterval = 1f / 20f;
        private float _nextSend;
        private bool _showHud = true;

        public override void OnInitializeMelon()
        {
            var cat = MelonPreferences.CreateCategory("PrecinctMP");
            _name = cat.CreateEntry("PlayerName", "Policier", "Pseudo affiché aux autres");
            _hostIp = cat.CreateEntry("HostIp", "127.0.0.1", "IP de l'hôte (pour F9)");
            _port = cat.CreateEntry("Port", 7777, "Port UDP");
            _playerObject = cat.CreateEntry("PlayerObjectName", "", "Nom du GameObject joueur si la détection auto échoue (voir F7)");
            _minVehicleMass = cat.CreateEntry("MinVehicleMass", 500f, "Masse minimale d'un Rigidbody pour être considéré comme une voiture");

            _session.StateReceived += _remotes.OnState;
            _session.PlayerJoined += (id, n) => { _remotes.Add(id, n); Feed($"{n} a rejoint la patrouille"); };
            _session.PlayerLeft += id => { _remotes.Remove(id); Feed($"Joueur {id} est parti"); };
            _session.ChatReceived += (id, t) => Feed($"{NameOf(id)} : {t}");
            _session.WorldReceived += _world.Apply;
            _session.ClientAccepted += _ => _world.ForceSendSoon();
            _session.IncidentsReceived += _sharedView.OnIncidents;
            _session.SuspectsReceived += _sharedView.OnSuspects;
#if IL2CPP
            CrimeSuppression.Install(HarmonyInstance);
#endif

            _joinDialog.OnJoin = (ip, port) =>
            {
                // Mémorise pour la prochaine fois
                _hostIp.Value = ip;
                _port.Value = port;
                MelonPreferences.Save();
                _remotes.Clear();
                _session.Join(ip, port, _name.Value);
            };

            _hostDialog.OnStart = port =>
            {
                _port.Value = port;
                MelonPreferences.Save();
                _remotes.Clear();
                _session.Host(port, _name.Value);
            };
            _hostDialog.OnStop = () => { _session.Stop(); _remotes.Clear(); };

            LoggerInstance.Msg("PrecinctMP chargé. F8 héberger, F9 rejoindre, F10 quitter, F7 diagnostic.");
        }

        public override void OnUpdate()
        {
            HandleKeys();
            _joinDialog.Tick();
            _hostDialog.Tick();

            _session.Poll();
            LogPlayerDetection();
            _local.Update(_playerObject.Value, _minVehicleMass.Value);
            _remotes.SetTemplate(_local.Player, _local.BodyAnimator);

            if (_session.IsHost && _world.TryCaptureForSend(out var ws)) _session.SendWorld(ws);
            if (!_session.IsActive && _world.Status.Length > 0) _world.Reset(); // session terminée

            // Monde partagé : l'hôte envoie ses crimes/suspects, le client coupe les siens
#if IL2CPP
            if (_session.IsHost) _hostCrimes.Tick(_session);
            bool clientConnected = _session.IsActive && !_session.IsHost && _session.IsConnected;
            CrimeSuppression.SetActive(clientConnected);
#endif
            if (!_session.IsActive)
            {
                if (_sharedView.SuspectCount > 0 || _sharedView.IncidentCount > 0) _sharedView.Clear();
#if IL2CPP
                if (_hostCrimes.IncidentCount > 0 || _hostCrimes.SuspectCount > 0) _hostCrimes.Reset();
#endif
            }
            _sharedView.Update();

            if (_session.IsConnected && Time.unscaledTime >= _nextSend)
            {
                _nextSend = Time.unscaledTime + SendInterval;
                if (_local.TryGetState(out var s)) _session.SendState(s);
            }

            _remotes.Update();
        }

        private bool _guiBroken;

        public override void OnGUI()
        {
            if ((!_showHud && !AnyDialogOpen) || _guiBroken) return;
            try { DrawHud(); }
            catch (System.Exception e)
            {
                // IMGUI peut être "strippé" dans les builds IL2CPP : on coupe le HUD
                // sans faire planter le reste du mod.
                _guiBroken = true;
                _joinDialog.Close();
                _hostDialog.Close();
                LoggerInstance.Warning("HUD désactivé (IMGUI indisponible) : " + e.Message);
            }
        }

        private void DrawHud()
        {
            if (!AnyDialogOpen)
            {
                _sharedView.DrawSuspectLabels();
                _sharedView.DrawHud(Screen.width - 350, 10);
            }
            if (_joinDialog.IsOpen) { _joinDialog.Draw(); return; }
            if (_hostDialog.IsOpen) { _hostDialog.Draw(); return; }
            _remotes.DrawLabels();

            string status = !_session.IsActive ? "hors ligne (F8 héberger / F9 rejoindre)"
                : _session.IsHost ? $"HÔTE port {_port.Value}"
                : _session.IsConnected ? $"connecté à {_hostIp.Value}"
                : "connexion...";
            string player = _local.Player == null ? "joueur NON détecté (F7)"
                : _local.Vehicle != null ? $"en voiture ({_local.VehicleName})" : "à pied";
            if (_local.Player != null) player += _local.UsingGameApi ? "" : " [mode générique]";

            GUI.Box(new Rect(10, 10, 330, 70 + (_feed.Count + (_world.Status.Length > 0 ? 1 : 0)) * 18), "PrecinctMP 0.3.0");
            GUI.Label(new Rect(20, 32, 310, 20), $"Réseau : {status}");
            GUI.Label(new Rect(20, 50, 310, 20), $"Local : {player} — {_remotes.Count} autre(s) joueur(s)");
            int line = 0;
            if (_world.Status.Length > 0)
                GUI.Label(new Rect(20, 72 + line++ * 18, 310, 20), "Monde : " + _world.Status);
            for (int i = 0; i < _feed.Count; i++)
                GUI.Label(new Rect(20, 72 + line++ * 18, 310, 20), _feed[i]);
        }

        public override void OnApplicationQuit()
        {
            _joinDialog.Close();
            _hostDialog.Close();
#if IL2CPP
            CrimeSuppression.SetActive(false);
#endif
            _session.Stop();
        }

        private void HandleKeys()
        {
            if (Hotkeys.Pressed(Hotkeys.F8))
            {
                if (_guiBroken) { _remotes.Clear(); _session.Host(_port.Value, _name.Value); } // pas d'IMGUI : démarrage direct
                else if (_hostDialog.IsOpen) _hostDialog.Close();
                else { _joinDialog.Close(); _hostDialog.Open(_port.Value, _session.IsActive && _session.IsHost); }
            }
            if (Hotkeys.Pressed(Hotkeys.F9))
            {
                if (_guiBroken) { _remotes.Clear(); _session.Join(_hostIp.Value, _port.Value, _name.Value); } // pas d'IMGUI : connexion directe
                else if (_joinDialog.IsOpen) _joinDialog.Close();
                else { _hostDialog.Close(); _joinDialog.Open(_hostIp.Value, _port.Value); }
            }
            if (Hotkeys.Pressed(Hotkeys.F10)) { _session.Stop(); _remotes.Clear(); }
            if (Hotkeys.Pressed(Hotkeys.F7)) SafeDump();
            if (Hotkeys.Pressed(Hotkeys.F11)) _showHud = !_showHud;
        }

        private void SafeDump()
        {
            try { Diagnostics.Dump(_local); }
            catch (System.Exception e) { LoggerInstance.Error("Diagnostic impossible : " + e); }
        }

        private bool _lastDetected, _lastInVehicle;

        /// <summary>Écrit dans le log quand la détection change (utile si le HUD est coupé).</summary>
        private void LogPlayerDetection()
        {
            bool det = _local.Player != null, veh = _local.Vehicle != null;
            if (det != _lastDetected) LoggerInstance.Msg(det ? $"Joueur détecté : {_local.Player.name} ({(_local.UsingGameApi ? "API du jeu" : "mode générique")})" : "Joueur perdu");
            if (veh != _lastInVehicle) LoggerInstance.Msg(veh ? $"Entré dans : {_local.VehicleName}" : "Sorti du véhicule");
            _lastDetected = det; _lastInVehicle = veh;
        }

        private string NameOf(byte id) => _session.Names.TryGetValue(id, out var n) ? n : "Joueur " + id;

        private void Feed(string line)
        {
            _feed.Add(line);
            if (_feed.Count > 5) _feed.RemoveAt(0);
        }
    }
}
