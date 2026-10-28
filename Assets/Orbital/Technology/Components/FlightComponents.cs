using Orbital.Core.Flight;

namespace Orbital.Technology.Components
{
    // Components of a craft for the Leopotam ecs world. Plain blittable data from Core/Flight: the systems only call the pure functions
    // of the flight core on them, so the same data can move to Jobs/Burst later. Replaces JetEngineComponent (kept only because
    // TestOrbit.prefab still references DynamicBodyAdapter).

    /// <summary>Physical state of the craft (position, velocity, orientation, rates, fuel, throttle, mode).</summary>
    public struct FlightStateComponent
    {
        public FlightState State;
    }

    /// <summary>Numbers the step needs besides the state: the craft, the ground of the body it is over, the body's gravity parameter.</summary>
    public struct FlightParamsComponent
    {
        public FlightParams Params;
    }

    /// <summary>Raw keys of this step, written by the input layer.</summary>
    public struct KeyStateComponent
    {
        public KeyState Keys;
    }

    /// <summary>The R edge and the RCS switch, kept between steps.</summary>
    public struct InputLatchComponent
    {
        public InputLatch Latch;
    }

    /// <summary>Control state of this step (axes, throttle commands, RCS switch), made from the keys.</summary>
    public struct ControlInputComponent
    {
        public ControlInput Input;
    }

    /// <summary>What the last step consumed, plus totals for the HUD.</summary>
    public struct StepReportComponent
    {
        public StepReport Report;
        public double TotalMainFuelUsed;
        public double TotalRcsFuelUsed;
        public double LastImpactSpeed; // m/s relative to the ground at the last touchdown, 0 before the first
    }

    /// <summary>Thrust segments as they would go to the server; here only counted (the network part is stage 2c).</summary>
    public struct ThrustRecorderComponent
    {
        public RecorderState State;
        public int SentCount;
        public double SentDuration;
        public ThrustSegment LastSent;
    }

    /// <summary>Marks the craft the local player flies.</summary>
    public struct PlayerControlled
    {
        public string Callsign;
    }
}
