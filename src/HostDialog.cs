using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// F8 : « DÉBUTER UNE SESSION MULTIJOUEUR ».
    /// Affiche les IP de l'hôte (à donner aux autres joueurs) et le port à ouvrir.
    /// Si une session est déjà hébergée, propose de l'arrêter.
    /// </summary>
    public class HostDialog : ModalDialog
    {
        protected override float Width => 420f;
        protected override float Height => 150f + Mathf.Max(1, _ips.Count) * 26f + 90f;
        protected override string Title => _running ? "SESSION MULTIJOUEUR EN COURS" : "DÉBUTER UNE SESSION MULTIJOUEUR";

        private readonly List<(string ip, string label)> _ips = new List<(string, string)>();
        private string _port = "", _error = "", _copied = "";
        private bool _running;

        // IP internet : récupérée une fois en arrière-plan, puis gardée en cache
        private static volatile string _publicIp;
        private static volatile string _publicStatus = "";
        private static bool _fetching;

        public System.Action<int> OnStart;
        public System.Action OnStop;

        public void Open(int port, bool alreadyHosting)
        {
            _port = port.ToString();
            _error = _copied = "";
            _running = alreadyHosting;
            RefreshIps();
            FetchPublicIp();
            Show();
        }

        protected override void DrawContent(float x, float y)
        {
            float row = y + 36;
            GUI.Label(new Rect(x + 20, row, Width - 40, 22), "IP de l'hôte à donner aux autres joueurs :");
            row += 26;

            // IP internet (pour jouer sans VPN : nécessite d'ouvrir le port sur la box)
            string pub = _publicIp;
            GUI.Label(new Rect(x + 30, row, 150, 22), pub ?? _publicStatus);
            GUI.Label(new Rect(x + 180, row, 150, 22), "Internet (box)");
            if (pub != null && GUI.Button(new Rect(x + Width - 90, row, 70, 22), _copied == pub ? "Copié" : "Copier"))
            {
                GUIUtility.systemCopyBuffer = pub;
                _copied = pub;
            }
            row += 30;

            if (_ips.Count == 0)
            {
                GUI.Label(new Rect(x + 30, row, Width - 60, 22), "Aucune adresse réseau trouvée.");
                row += 26;
            }
            foreach (var (ip, label) in _ips)
            {
                GUI.Label(new Rect(x + 30, row, 150, 22), ip);
                GUI.Label(new Rect(x + 180, row, 150, 22), label);
                if (GUI.Button(new Rect(x + Width - 90, row, 70, 22), _copied == ip ? "Copié" : "Copier"))
                {
                    GUIUtility.systemCopyBuffer = ip;
                    _copied = ip;
                }
                row += 26;
            }

            row += 6;
            GUI.Label(new Rect(x + 20, row, 80, 22), "Port UDP");
            if (_running) GUI.Label(new Rect(x + 110, row, 90, 22), _port);
            else _port = GUI.TextField(new Rect(x + 110, row, 90, 22), _port, 5);
            row += 26;

            GUI.Label(new Rect(x + 20, row, Width - 40, 22),
                _error.Length > 0 ? "Erreur : " + _error : "IP Internet : ouvre ce port en UDP sur ta box (redirection vers ce PC).");

            string main = _running ? "Arrêter la session" : "Démarrer";
            if (GUI.Button(new Rect(x + 20, y + Height - 46, 170, 30), main)) OnSubmit();
            if (GUI.Button(new Rect(x + Width - 190, y + Height - 46, 170, 30), _running ? "Fermer" : "Annuler")) Close();
        }

        protected override void OnSubmit()
        {
            if (_running) { Close(); OnStop?.Invoke(); return; }
            if (!PortField.TryParse(_port, out int port)) { _error = "Port invalide (1 à 65535)."; return; }
            Close();
            OnStart?.Invoke(port);
        }

        private static readonly string[] PublicIpServices =
        {
            "https://api.ipify.org",
            "https://icanhazip.com",
            "https://ifconfig.me/ip",
        };

        /// <summary>Demande l'IP publique à un service web (en tâche de fond, sans bloquer le jeu).</summary>
        private static void FetchPublicIp()
        {
            if (_publicIp != null || _fetching) return;
            _fetching = true;
            _publicStatus = "recherche...";
            Task.Run(async () =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                foreach (var url in PublicIpServices)
                {
                    try
                    {
                        string ip = (await http.GetStringAsync(url)).Trim();
                        if (System.Net.IPAddress.TryParse(ip, out _)) { _publicIp = ip; break; }
                    }
                    catch { /* service suivant */ }
                }
                if (_publicIp == null) _publicStatus = "indisponible";
                _fetching = false;
            });
        }

        /// <summary>Liste les IPv4 de la machine et devine leur type (VPN de jeu, réseau local...).</summary>
        private void RefreshIps()
        {
            _ips.Clear();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254.")) continue; // auto-config sans réseau
                        _ips.Add((ip, Describe(ip, ni.Description)));
                    }
                }
                // VPN de jeu en premier (le plus simple pour jouer entre amis)
                _ips.Sort((a, b) => Rank(a.label).CompareTo(Rank(b.label)));
            }
            catch (System.Exception e) { _error = "lecture des IP impossible : " + e.Message; }
        }

        private static string Describe(string ip, string adapter)
        {
            string a = (adapter ?? "").ToLowerInvariant();
            if (a.Contains("radmin") || ip.StartsWith("26.")) return "Radmin VPN";
            if (a.Contains("zerotier")) return "ZeroTier";
            if (a.Contains("tailscale") || ip.StartsWith("100.")) return "Tailscale";
            if (a.Contains("hamachi") || ip.StartsWith("25.")) return "Hamachi";
            if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || IsPrivate172(ip)) return "Réseau local";
            return "Autre";
        }

        private static int Rank(string label) => label == "Réseau local" ? 1 : label == "Autre" ? 2 : 0;

        private static bool IsPrivate172(string ip)
        {
            var p = ip.Split('.');
            return p.Length == 4 && p[0] == "172" && int.TryParse(p[1], out int b) && b >= 16 && b <= 31;
        }
    }
}
