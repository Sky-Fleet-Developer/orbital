using System;
using Newtonsoft.Json.Linq;
using Orbital.Core.Config;
using Orbital.Core.Flight;

namespace Orbital.Core.Net
{
    /// <summary>
    /// Between the protocol DTOs and the flight core (stage 2c.4): server state to <see cref="FlightState"/> and back, recorded thrust
    /// segments and attitude slices to requests, the craft's flight configuration from its snapshot. Quaternions are [w,x,y,z] on the
    /// wire and are normalised before they are sent (the server checks the norm to 1e-6).
    /// </summary>
    public static class StateMapper
    {
        public static double[] Arr(Vec3 v) => new[] { v.X, v.Y, v.Z };
        public static double[] Arr(DQuat q) => new[] { q.W, q.X, q.Y, q.Z };

        public static Vec3 Vec(double[] a, Vec3 fallback) => a != null && a.Length == 3 ? new Vec3(a[0], a[1], a[2]) : fallback;
        public static DQuat Quat(double[] a, DQuat fallback) => a != null && a.Length == 4 ? new DQuat(a[0], a[1], a[2], a[3]) : fallback;

        public static int ModeOf(CraftModeDto mode)
        {
            switch (mode)
            {
                case CraftModeDto.Landed: return FlightMode.Landed;
                case CraftModeDto.Crashed: return FlightMode.Crashed;
                default: return FlightMode.Flying;
            }
        }

        public static CraftModeDto ModeOf(int mode)
        {
            switch (mode)
            {
                case FlightMode.Landed: return CraftModeDto.Landed;
                case FlightMode.Crashed: return CraftModeDto.Crashed;
                default: return CraftModeDto.Flying;
            }
        }

        /// <summary>The whole flight state as the server sees it. The throttle is 0 (the server state has none), as is the RCS unloading flag.</summary>
        public static FlightState ToFlightState(CraftStateDto s)
        {
            FlightState f = default;
            f.Orientation = DQuat.Identity;
            f.SasTarget = DQuat.Identity;
            ApplyAuthoritative(s, ref f);
            ApplyAttitude(s, ref f);
            return f;
        }

        /// <summary>
        /// Only what the server is the authority for (stage 2c plan, section 1): position, velocity, mode, both tanks, ground coordinates.
        /// Attitude, rates, gyro and SAS stay the client's.
        /// </summary>
        public static void ApplyAuthoritative(CraftStateDto s, ref FlightState f)
        {
            f.Position = Vec(s.Position, f.Position);
            f.Velocity = Vec(s.Velocity, f.Velocity);
            f.Mode = ModeOf(s.Mode);
            f.MainFuel = s.Fuel;
            f.RcsFuel = s.RcsFuel;
            if (s.Latitude.HasValue) f.Latitude = s.Latitude.Value;
            if (s.Longitude.HasValue) f.Longitude = s.Longitude.Value;
        }

        /// <summary>The server's copy of the attitude (after a rejection, at login, on a hard resynchronisation).</summary>
        public static void ApplyAttitude(CraftStateDto s, ref FlightState f)
        {
            f.Orientation = DQuat.Normalize(Quat(s.Orientation, f.Orientation));
            f.Omega = Vec(s.AngularVelocity, Vec3.Zero);
            f.GyroH = Vec(s.GyroH, Vec3.Zero);
            ApplySas(s.Sas, ref f);
        }

        public static void ApplySas(SasDto sas, ref FlightState f)
        {
            if (sas == null || !sas.On)
            {
                f.SasOn = 0;
                f.SasPhase = SasPhase.Hold;
                f.SasTarget = DQuat.Identity;
                f.SasIntegral = Vec3.Zero;
                return;
            }
            f.SasOn = 1;
            f.SasPhase = sas.Phase;
            f.SasTarget = DQuat.Normalize(Quat(sas.Target, DQuat.Identity));
            f.SasIntegral = Vec(sas.Integral, Vec3.Zero);
        }

        /// <summary>A flight state as a server state (tests, ghosts): only the fields <see cref="ToFlightState"/> reads.</summary>
        public static CraftStateDto FromFlightState(in FlightState f, double time, string craftId)
        {
            bool landed = f.Mode == FlightMode.Landed;
            return new CraftStateDto
            {
                CraftId = craftId, Time = time, Mode = ModeOf(f.Mode),
                Position = Arr(f.Position), Velocity = Arr(f.Velocity),
                Fuel = f.MainFuel, RcsFuel = f.RcsFuel,
                Orientation = Arr(f.Orientation), AngularVelocity = Arr(f.Omega), GyroH = Arr(f.GyroH),
                Sas = SasOf(f.SasOn, f.SasPhase, f.SasTarget, f.SasIntegral),
                Latitude = landed ? f.Latitude : (double?) null, Longitude = landed ? f.Longitude : (double?) null,
            };
        }

        /// <summary>The SAS block of a message; null when the SAS is off (the server reads null as off).</summary>
        public static SasDto SasOf(int on, int phase, DQuat target, Vec3 integral) =>
            on == 0 ? null : new SasDto { On = true, Phase = phase, Target = Arr(DQuat.Normalize(target)), Integral = Arr(integral) };

        public static SasDto SasOf(in AttitudeSample a) => SasOf(a.SasOn, a.SasPhase, a.SasTarget, a.SasIntegral);

        /// <summary>
        /// A closed thrust segment as a maneuver request with the attitude at its start and end. <paramref name="rcsFuelUsed"/> is the RCS
        /// propellant since the last attitude record the server accepted (the sender keeps that baseline).
        /// </summary>
        public static ManeuverRequestDto ToManeuver(in ThrustSegment seg, long seq, string craftId, double rcsFuelUsed)
        {
            return new ManeuverRequestDto
            {
                Direction = Arr(seg.Direction), Throttle = Math.Min(1.0, seg.Throttle), StartTime = seg.StartTime, Duration = seg.Duration,
                Orientation = Arr(DQuat.Normalize(seg.Start.Orientation)), Omega = Arr(seg.Start.Omega), GyroH = Arr(seg.Start.GyroH),
                EndOrientation = Arr(DQuat.Normalize(seg.End.Orientation)), EndOmega = Arr(seg.End.Omega), EndGyroH = Arr(seg.End.GyroH),
                RcsFuelUsed = Math.Max(0.0, rcsFuelUsed),
                Sas = SasOf(seg.Start), EndSas = SasOf(seg.End),
                Seq = seq, CraftId = craftId,
            };
        }

        public static AttitudeReportDto ToAttitudeReport(in AttitudeSample a, long seq, string craftId, double rcsFuelUsed)
        {
            return new AttitudeReportDto
            {
                Time = a.Time, Orientation = Arr(DQuat.Normalize(a.Orientation)), Omega = Arr(a.Omega), GyroH = Arr(a.GyroH),
                RcsFuelUsed = Math.Max(0.0, rcsFuelUsed), Sas = SasOf(a), Seq = seq, CraftId = craftId,
            };
        }

        /// <summary>
        /// The flight configuration of a craft as the server uses it: its stored copy (<c>spec.flight</c>, partial JSON on top of the
        /// defaults, or <paramref name="fallback"/> when absent), then the four spec numbers on top (server CraftSpec.EffectiveFlight).
        /// </summary>
        public static FlightConfig FlightConfigOf(CraftSnapshotDto snapshot, in FlightConfig fallback)
        {
            CraftSpecDto spec = snapshot?.Spec;
            if (spec == null) return fallback;
            FlightConfig f = spec.Flight != null ? GameConfigLoader.ParseFlight(spec.Flight.ToString(Newtonsoft.Json.Formatting.None)) : fallback;
            f.DryMass = spec.DryMass;
            f.MainFuelCapacity = spec.MaxFuel;
            f.Engine.MaxThrust = spec.ThrustN;
            f.Engine.Isp = spec.IspSeconds;
            return f;
        }
    }
}
