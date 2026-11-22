using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PrecinctMP
{
    /// <summary>
    /// Touches lues directement via Windows (GetAsyncKeyState).
    /// Beaucoup de jeux IL2CPP utilisent le nouvel Input System d'Unity, ce qui
    /// fait planter UnityEngine.Input : cette méthode marche dans tous les cas.
    /// </summary>
    public static class Hotkeys
    {
        public const int F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A;

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern System.IntPtr GetForegroundWindow();
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(System.IntPtr hWnd, out uint pid);

        private static readonly Dictionary<int, bool> _wasDown = new Dictionary<int, bool>();

        /// <summary>Vrai uniquement à l'image où la touche est enfoncée.</summary>
        public static bool Pressed(int vk)
        {
            bool down = HasFocus() && (GetAsyncKeyState(vk) & 0x8000) != 0;
            _wasDown.TryGetValue(vk, out bool was);
            _wasDown[vk] = down;
            return down && !was;
        }

        /// <summary>Ignore les touches quand le jeu n'est pas au premier plan.</summary>
        private static bool HasFocus()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            return pid == GetCurrentProcessId();
        }
    }
}
