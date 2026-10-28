namespace Orbital.Core.Flight
{
    /// <summary>Main engine numbers. Defaults match the server craft (Propagator/ServerConfig.DefaultCraft).</summary>
    public struct EngineConfig
    {
        public double MaxThrust;      // N at throttle 1
        public double Isp;            // s
        public double ThrottleRate;   // 1/s, smooth throttle change (shift/ctrl)
    }

    /// <summary>RCS: maximum torque per body axis (both directions) and the propellant it burns.</summary>
    public struct RcsConfig
    {
        public Vec3 MaxTorque;        // N*m about body X, Y, Z
        public double Isp;            // s (cold gas / monopropellant)
        public double Arm;            // m, lever arm used to turn torque into thruster force
    }

    /// <summary>Control moment gyro / reaction wheels: always on, limited torque and limited stored momentum.</summary>
    public struct GyroConfig
    {
        public double MaxTorque;          // N*m per axis
        public double MomentumCapacity;   // N*m*s per axis, H_max
        public double SoftZone;           // fraction of capacity over which the torque fades out near saturation
        public double DumpStart;          // |H|/capacity above which RCS unloading starts
        public double DumpStop;           // ... and below which it stops
        public double DumpTorqueFraction; // unloading torque as a fraction of the RCS maximum
    }

    /// <summary>
    /// Everything about the craft that is a number: masses, inertia, engine, RCS, gyro, disturbances.
    /// Body axes: +Y is the long axis (nose and thrust direction), pitch is rotation about X, yaw about Z, roll about Y.
    /// </summary>
    public struct FlightConfig
    {
        public double DryMass;            // kg
        public double MainFuelCapacity;   // kg
        public double RcsPropellantCapacity; // kg
        public Vec3 Inertia;              // principal moments (Ixx, Iyy, Izz), kg*m^2
        public EngineConfig Engine;
        public RcsConfig Rcs;
        public GyroConfig Gyro;
        public Vec3 ThrustOffset;         // m, lateral offset of the thrust line from the center of mass (body frame)
        public double CgHeight;           // m, center of mass above the contact point when standing on legs

        /// <summary>Starting numbers from Docs/Stage2-Plan.md section 3: rocket about 45 t, gyro 8000 N*m, RCS 5e4 N*m.</summary>
        public static FlightConfig Default()
        {
            return new FlightConfig
            {
                DryMass = 5000,
                MainFuelCapacity = 40000,
                RcsPropellantCapacity = 200,
                Inertia = new Vec3(1.7e6, 5e4, 1.7e6),
                Engine = new EngineConfig { MaxThrust = 600000, Isp = 450, ThrottleRate = 0.25 },
                Rcs = new RcsConfig { MaxTorque = new Vec3(5e4, 1.2e4, 5e4), Isp = 200, Arm = 2.0 },
                Gyro = new GyroConfig
                {
                    MaxTorque = 8000, MomentumCapacity = 3e4, SoftZone = 0.1,
                    DumpStart = 0.8, DumpStop = 0.2, DumpTorqueFraction = 0.5,
                },
                ThrustOffset = new Vec3(0.05, 0, 0),
                CgHeight = 8.0,
            };
        }
    }
}
