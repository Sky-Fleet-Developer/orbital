using Orbital.Core.Flight;

namespace Orbital.Technology
{
    /// <summary>
    /// Time and switches shared by the flight systems (injected into them by type). Time is the game time of the step start and Dt the step length;
    /// the owner advances them, so the result of a run depends only on the sequence of steps, not on the frame rate.
    /// </summary>
    public sealed class FlightClock
    {
        public double Time;
        public double Dt = 0.02;
        /// <summary>Hold a landed craft upright (<see cref="LandedSupport"/>); on by default, see the stage 2b notes.</summary>
        public bool GroundSupport = true;
        public RecorderConfig Recorder = RecorderConfig.Default();
    }
}
