namespace Orbital.Core.Flight
{
    /// <summary>The keys of the flight controls (the fields of <see cref="KeyState"/>), as a list the input layer can walk through.</summary>
    public enum ControlKey
    {
        W, S, A, D, Q, E, R, Shift, Ctrl, X, Z, T,
    }

    /// <summary>
    /// Engine-free half of the keyboard input: which key sets which field of <see cref="KeyState"/>, the default binding paths (as the new
    /// Input System writes them) and the accumulation of presses between two simulation steps.
    /// </summary>
    public static class ControlKeys
    {
        public const int Count = 12;

        private static readonly string[] Paths =
        {
            "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d", "<Keyboard>/q", "<Keyboard>/e", "<Keyboard>/r",
            "<Keyboard>/shift", "<Keyboard>/ctrl", "<Keyboard>/x", "<Keyboard>/z", "<Keyboard>/t",
        };

        /// <summary>Default Input System binding path of the key. "shift" and "ctrl" are the combined left/right controls.</summary>
        public static string DefaultPath(ControlKey key) => Paths[(int) key];

        public static void Set(ref KeyState state, ControlKey key, bool pressed)
        {
            switch (key)
            {
                case ControlKey.W: state.W = pressed; break;
                case ControlKey.S: state.S = pressed; break;
                case ControlKey.A: state.A = pressed; break;
                case ControlKey.D: state.D = pressed; break;
                case ControlKey.Q: state.Q = pressed; break;
                case ControlKey.E: state.E = pressed; break;
                case ControlKey.R: state.R = pressed; break;
                case ControlKey.Shift: state.Shift = pressed; break;
                case ControlKey.Ctrl: state.Ctrl = pressed; break;
                case ControlKey.X: state.X = pressed; break;
                case ControlKey.Z: state.Z = pressed; break;
                default: state.T = pressed; break;
            }
        }

        public static bool Get(in KeyState state, ControlKey key)
        {
            switch (key)
            {
                case ControlKey.W: return state.W;
                case ControlKey.S: return state.S;
                case ControlKey.A: return state.A;
                case ControlKey.D: return state.D;
                case ControlKey.Q: return state.Q;
                case ControlKey.E: return state.E;
                case ControlKey.R: return state.R;
                case ControlKey.Shift: return state.Shift;
                case ControlKey.Ctrl: return state.Ctrl;
                case ControlKey.X: return state.X;
                case ControlKey.Z: return state.Z;
                default: return state.T;
            }
        }

        /// <summary>
        /// A key counts as pressed if it was down in either state. The input layer polls every frame and the simulation steps at its own rate:
        /// without this a tap shorter than the gap between two steps (an R press at 60 fps against a 50 Hz step) would be lost.
        /// </summary>
        public static KeyState Or(in KeyState a, in KeyState b)
        {
            KeyState r;
            r.W = a.W || b.W;
            r.S = a.S || b.S;
            r.A = a.A || b.A;
            r.D = a.D || b.D;
            r.Q = a.Q || b.Q;
            r.E = a.E || b.E;
            r.R = a.R || b.R;
            r.Shift = a.Shift || b.Shift;
            r.Ctrl = a.Ctrl || b.Ctrl;
            r.X = a.X || b.X;
            r.Z = a.Z || b.Z;
            r.T = a.T || b.T;
            return r;
        }
    }
}
