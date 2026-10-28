using System;

namespace Orbital.Core.Flight
{
    /// <summary>Numbers a pilot reads from the instruments, derived from a <see cref="FlightState"/>. No state of its own.</summary>
    public struct FlightTelemetry
    {
        public double Altitude;           // m above the ground
        public double GroundSpeed;        // m/s relative to the rotating ground
        public double InertialSpeed;      // m/s in the parent-centered inertial frame
        public double VerticalSpeed;      // m/s along the local vertical, up positive (relative to the ground)
        public double HorizontalSpeed;    // m/s across the local vertical, relative to the ground
        public double TiltDegrees;        // angle between the nose (+Y) and the local vertical
        public double PitchRateDegPerSec; // body rates, deg/s
        public double RollRateDegPerSec;
        public double YawRateDegPerSec;
        public double Mass;               // kg
        public double Thrust;             // N at the current throttle
        public double ThrustToWeight;     // thrust / local weight
        public double MainFuelFraction;   // 0..1 of the tank
        public double RcsFuelFraction;    // 0..1
        public double GyroFraction;       // largest stored momentum component / capacity, 0..1
        public bool SasOn;
        public int SasPhase;              // SasPhase
        public double SasErrorDegrees;    // angle between the orientation and the SAS target (0 when off)

        public static FlightTelemetry Compute(in FlightState s, in FlightParams p)
        {
            FlightTelemetry t = default;
            double r = Vec3.Length(s.Position);
            Vec3 up = r > 0 ? s.Position / r : Vec3.UnitY;
            t.Altitude = Surface.Altitude(p.Surface, s.Position);
            Vec3 rel = s.Velocity - Surface.GroundVelocity(p.Surface, s.Position);
            t.GroundSpeed = Vec3.Length(rel);
            t.InertialSpeed = Vec3.Length(s.Velocity);
            t.VerticalSpeed = Vec3.Dot(rel, up);
            double horizontalSq = Vec3.LengthSquared(rel) - t.VerticalSpeed * t.VerticalSpeed;
            t.HorizontalSpeed = horizontalSq > 0 ? Math.Sqrt(horizontalSq) : 0.0;
            Vec3 nose = DQuat.Rotate(s.Orientation, Vec3.UnitY);
            const double radToDeg = 180.0 / Math.PI;
            t.TiltDegrees = Vec3.Angle(nose, up) * radToDeg;
            t.PitchRateDegPerSec = s.Omega.X * radToDeg;
            t.RollRateDegPerSec = s.Omega.Y * radToDeg;
            t.YawRateDegPerSec = s.Omega.Z * radToDeg;
            t.Mass = FlightKernel.TotalMass(p.Craft, s);
            t.Thrust = EngineModel.Thrust(p.Craft.Engine, s.Throttle);
            double weight = r > 0 ? t.Mass * p.Nu / (r * r) : 0.0;
            t.ThrustToWeight = weight > 0 ? t.Thrust / weight : 0.0;
            t.MainFuelFraction = Fraction(s.MainFuel, p.Craft.MainFuelCapacity);
            t.RcsFuelFraction = Fraction(s.RcsFuel, p.Craft.RcsPropellantCapacity);
            double h = Math.Max(Math.Abs(s.GyroH.X), Math.Max(Math.Abs(s.GyroH.Y), Math.Abs(s.GyroH.Z)));
            t.GyroFraction = Fraction(h, p.Craft.Gyro.MomentumCapacity);
            t.SasOn = s.SasOn != 0;
            t.SasPhase = s.SasPhase;
            t.SasErrorDegrees = SasController.ErrorAngle(s) * radToDeg;
            return t;
        }

        private static double Fraction(double value, double capacity)
        {
            if (capacity <= 0) return 0.0;
            double f = value / capacity;
            return f < 0 ? 0.0 : (f > 1 ? 1.0 : f);
        }
    }
}
