using UnityEngine;

namespace PrecinctMP
{
    /// <summary>F9 : « REJOINDRE UNE SESSION » (IP + port de l'hôte).</summary>
    public class JoinDialog : ModalDialog
    {
        protected override float Width => 360f;
        protected override float Height => 190f;
        protected override string Title => "REJOINDRE UNE SESSION";

        private string _ip = "", _port = "", _error = "";

        /// <summary>Appelé quand l'utilisateur valide : (ip, port).</summary>
        public System.Action<string, int> OnJoin;

        public void Open(string ip, int port)
        {
            _ip = ip ?? "";
            _port = port.ToString();
            _error = "";
            Show();
        }

        protected override void DrawContent(float x, float y)
        {
            GUI.Label(new Rect(x + 20, y + 40, 80, 22), "Adresse IP");
            GUI.SetNextControlName("mp_ip");
            _ip = GUI.TextField(new Rect(x + 110, y + 40, Width - 130, 22), _ip, 64);

            GUI.Label(new Rect(x + 20, y + 72, 80, 22), "Port");
            _port = GUI.TextField(new Rect(x + 110, y + 72, 90, 22), _port, 5);

            if (_error.Length > 0)
                GUI.Label(new Rect(x + 20, y + 102, Width - 40, 22), "Erreur : " + _error);

            if (GUI.Button(new Rect(x + 20, y + Height - 46, 150, 30), "Rejoindre")) OnSubmit();
            if (GUI.Button(new Rect(x + Width - 170, y + Height - 46, 150, 30), "Annuler")) Close();

            if (FocusPending) { GUI.FocusControl("mp_ip"); FocusPending = false; }
        }

        protected override void OnSubmit()
        {
            string ip = (_ip ?? "").Trim();
            if (ip.Length == 0) { _error = "Indique l'adresse IP de l'hôte."; return; }
            if (!PortField.TryParse(_port, out int port)) { _error = "Port invalide (1 à 65535)."; return; }
            Close();
            OnJoin?.Invoke(ip, port);
        }
    }

    internal static class PortField
    {
        public static bool TryParse(string text, out int port)
            => int.TryParse((text ?? "").Trim(), out port) && port >= 1 && port <= 65535;
    }
}
