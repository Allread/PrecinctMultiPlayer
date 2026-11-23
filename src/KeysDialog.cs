using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// F6 : « TOUCHES DU MOD ». Clique sur « Modifier » puis appuie sur une lettre
    /// ou un chiffre. Échap annule la saisie en cours. Les touches sont enregistrées
    /// dans MelonPreferences.cfg.
    /// </summary>
    public class KeysDialog : ModalDialog
    {
        protected override float Width => 440f;
        protected override float Height => 300f;
        protected override string Title => "TOUCHES DU MOD";

        private static readonly string[] Labels = { "Ordre de se rendre", "Plaquer", "Menotter", "Fouiller" };
        private static readonly string[] Defaults = { "Y", "U", "I", "O" };

        private readonly string[] _keys = new string[4];
        private int _listening = -1;
        private string _error = "";

        /// <summary>Appelé à l'enregistrement avec les 4 touches (ordre de Labels).</summary>
        public System.Action<string[]> OnSave;

        public void Open(KeyBindings current)
        {
            _keys[0] = current.Surrender;
            _keys[1] = current.Tackle;
            _keys[2] = current.Restrain;
            _keys[3] = current.Search;
            _listening = -1;
            _error = "";
            Show();
        }

        protected override bool HandleKey(Event e)
        {
            if (_listening < 0) return false;
            if (e.keyCode == KeyCode.Escape) { _listening = -1; return true; }

            string key = ToKey(e.keyCode);
            if (key == null)
            {
                if (e.keyCode != KeyCode.None) _error = "Seules les lettres (A-Z) et les chiffres (0-9) sont acceptés.";
                return true;
            }
            _keys[_listening] = key;
            _listening = -1;
            _error = "";
            return true;
        }

        protected override void DrawContent(float x, float y)
        {
            float row = y + 40;
            GUI.Label(new Rect(x + 20, row, Width - 40, 22), "Actions du client sur les suspects de l'hôte :");
            row += 30;

            for (int i = 0; i < Labels.Length; i++)
            {
                GUI.Label(new Rect(x + 30, row, 170, 24), Labels[i]);
                string shown = _listening == i ? "Appuie sur une touche…" : $"[ {_keys[i]} ]";
                GUI.Label(new Rect(x + 200, row, 140, 24), shown);
                if (_listening < 0 && GUI.Button(new Rect(x + Width - 110, row, 90, 24), "Modifier"))
                {
                    _listening = i;
                    _error = "";
                }
                row += 32;
            }

            GUI.Label(new Rect(x + 20, row + 4, Width - 40, 22),
                _error.Length > 0 ? "Erreur : " + _error
                : _listening >= 0 ? "Échap pour annuler la saisie."
                : "Évite les touches déjà utilisées par le jeu.");

            if (_listening >= 0) return;
            float by = y + Height - 46;
            if (GUI.Button(new Rect(x + 20, by, 125, 30), "Enregistrer")) OnSubmit();
            if (GUI.Button(new Rect(x + 157, by, 125, 30), "Par défaut")) { Defaults.CopyTo(_keys, 0); _error = ""; }
            if (GUI.Button(new Rect(x + Width - 145, by, 125, 30), "Annuler")) Close();
        }

        protected override void OnSubmit()
        {
            if (_listening >= 0) return;
            for (int i = 0; i < _keys.Length; i++)
                for (int j = i + 1; j < _keys.Length; j++)
                    if (_keys[i] == _keys[j])
                    {
                        _error = $"« {Labels[i]} » et « {Labels[j]} » ont la même touche ({_keys[i]}).";
                        return;
                    }
            Close();
            OnSave?.Invoke((string[])_keys.Clone());
        }

        /// <summary>Lettre ou chiffre correspondant à la touche, sinon null.</summary>
        private static string ToKey(KeyCode k)
        {
            if (k >= KeyCode.A && k <= KeyCode.Z) return ((char)('A' + (k - KeyCode.A))).ToString();
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((char)('0' + (k - KeyCode.Alpha0))).ToString();
            return null;
        }
    }
}
