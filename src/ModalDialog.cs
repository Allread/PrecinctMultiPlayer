using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// Base des fenêtres du mod : libère la souris, bloque les commandes du jeu
    /// (PlayerInput.SetPlayerInputDisabled) et gère Entrée / Échap.
    /// </summary>
    public abstract class ModalDialog
    {
        public bool IsOpen { get; private set; }

        private CursorLockMode _prevLock;
        private bool _prevVisible;
        protected bool FocusPending;

        protected abstract float Width { get; }
        protected abstract float Height { get; }
        protected abstract string Title { get; }

        protected void Show()
        {
            if (IsOpen) return;
            IsOpen = true;
            FocusPending = true;
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            ApplyModalState();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
#if IL2CPP
            GameApi.SetInputBlocked(false);
#endif
        }

        /// <summary>À appeler depuis OnGUI.</summary>
        public void Draw()
        {
            if (!IsOpen) return;

            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.Use(); OnSubmit(); return; }
                if (e.keyCode == KeyCode.Escape) { e.Use(); Close(); return; }
            }

            float x = (Screen.width - Width) / 2f, y = (Screen.height - Height) / 2f;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.Box(new Rect(x, y, Width, Height), Title);
            DrawContent(x, y);
        }

        /// <summary>À appeler depuis OnUpdate : le jeu peut réactiver souris/commandes à tout moment.</summary>
        public void Tick()
        {
            if (IsOpen) ApplyModalState();
        }

        private static void ApplyModalState()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
#if IL2CPP
            GameApi.SetInputBlocked(true);
#endif
        }

        protected abstract void DrawContent(float x, float y);
        protected abstract void OnSubmit();
    }
}
