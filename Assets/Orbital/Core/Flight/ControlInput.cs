namespace Orbital.Core.Flight
{
    /// <summary>Raw pressed/not pressed state of the keys the flight controls use. Filled by the input layer, no Unity types here.</summary>
    public struct KeyState
    {
        public bool W, S, A, D, Q, E, R;
        public bool Shift, Ctrl, X, Z;
    }

    /// <summary>What has to be remembered between frames: the R key edge and the RCS switch.</summary>
    public struct InputLatch
    {
        public bool PreviousR;
        public bool RcsOn;
    }

    /// <summary>Control state for one step. Axes are in [-1, 1] and mean right-handed rotation about the body axis.</summary>
    public struct ControlInput
    {
        public double Pitch;          // about body X: W +1, S -1
        public double Yaw;            // about body Z: E +1, Q -1
        public double Roll;           // about body Y (long axis): D +1, A -1
        public double ThrottleSmooth; // +1 shift, -1 ctrl
        public bool ThrottleFull;     // X
        public bool ThrottleZero;     // Z (wins over X)
        public bool RcsOn;            // toggled by an R press
    }

    public static class InputMapper
    {
        /// <summary>Maps keys to axes; R switches the RCS on a press (not while held). Opposite keys cancel.</summary>
        public static ControlInput Map(in KeyState keys, ref InputLatch latch)
        {
            if (keys.R && !latch.PreviousR) latch.RcsOn = !latch.RcsOn;
            latch.PreviousR = keys.R;
            ControlInput c;
            c.Pitch = Axis(keys.W, keys.S);
            c.Roll = Axis(keys.D, keys.A);
            c.Yaw = Axis(keys.E, keys.Q);
            c.ThrottleSmooth = Axis(keys.Shift, keys.Ctrl);
            c.ThrottleFull = keys.X;
            c.ThrottleZero = keys.Z;
            c.RcsOn = latch.RcsOn;
            return c;
        }

        private static double Axis(bool plus, bool minus) => (plus ? 1.0 : 0.0) - (minus ? 1.0 : 0.0);

        /// <summary>The same axes as a body-frame vector (X pitch, Y roll, Z yaw).</summary>
        public static Vec3 AxesVector(in ControlInput c) => new Vec3(c.Pitch, c.Roll, c.Yaw);
    }
}
