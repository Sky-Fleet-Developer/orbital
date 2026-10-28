using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Structure of arrays for many crafts: one array per field. This is the layout Jobs/Burst (NativeArray) or a GPU buffer wants.
    /// The arrays are only containers; all math is in <see cref="FlightKernel"/>.
    /// </summary>
    public ref struct FlightSoA
    {
        public Span<double> PosX, PosY, PosZ, VelX, VelY, VelZ;
        public Span<double> QW, QX, QY, QZ;
        public Span<double> OmX, OmY, OmZ;
        public Span<double> HX, HY, HZ;
        public Span<double> MainFuel, RcsFuel, Throttle, Lat, Lon;
        public Span<int> Mode, Dumping;
        public Span<int> SasOn, SasPhase;
        public Span<double> SasQW, SasQX, SasQY, SasQZ, SasIX, SasIY, SasIZ;

        public int Length => PosX.Length;

        public static FlightSoA Allocate(int n)
        {
            FlightSoA a = default;
            a.PosX = new double[n]; a.PosY = new double[n]; a.PosZ = new double[n];
            a.VelX = new double[n]; a.VelY = new double[n]; a.VelZ = new double[n];
            a.QW = new double[n]; a.QX = new double[n]; a.QY = new double[n]; a.QZ = new double[n];
            a.OmX = new double[n]; a.OmY = new double[n]; a.OmZ = new double[n];
            a.HX = new double[n]; a.HY = new double[n]; a.HZ = new double[n];
            a.MainFuel = new double[n]; a.RcsFuel = new double[n]; a.Throttle = new double[n]; a.Lat = new double[n]; a.Lon = new double[n];
            a.Mode = new int[n]; a.Dumping = new int[n];
            a.SasOn = new int[n]; a.SasPhase = new int[n];
            a.SasQW = new double[n]; a.SasQX = new double[n]; a.SasQY = new double[n]; a.SasQZ = new double[n];
            a.SasIX = new double[n]; a.SasIY = new double[n]; a.SasIZ = new double[n];
            return a;
        }

        public FlightState Load(int i)
        {
            FlightState s;
            s.Position = new Vec3(PosX[i], PosY[i], PosZ[i]);
            s.Velocity = new Vec3(VelX[i], VelY[i], VelZ[i]);
            s.Orientation = new DQuat(QW[i], QX[i], QY[i], QZ[i]);
            s.Omega = new Vec3(OmX[i], OmY[i], OmZ[i]);
            s.GyroH = new Vec3(HX[i], HY[i], HZ[i]);
            s.MainFuel = MainFuel[i];
            s.RcsFuel = RcsFuel[i];
            s.Throttle = Throttle[i];
            s.Mode = Mode[i];
            s.Dumping = Dumping[i];
            s.Latitude = Lat[i];
            s.Longitude = Lon[i];
            s.SasOn = SasOn[i];
            s.SasPhase = SasPhase[i];
            s.SasTarget = new DQuat(SasQW[i], SasQX[i], SasQY[i], SasQZ[i]);
            s.SasIntegral = new Vec3(SasIX[i], SasIY[i], SasIZ[i]);
            return s;
        }

        public void Store(int i, in FlightState s)
        {
            PosX[i] = s.Position.X; PosY[i] = s.Position.Y; PosZ[i] = s.Position.Z;
            VelX[i] = s.Velocity.X; VelY[i] = s.Velocity.Y; VelZ[i] = s.Velocity.Z;
            QW[i] = s.Orientation.W; QX[i] = s.Orientation.X; QY[i] = s.Orientation.Y; QZ[i] = s.Orientation.Z;
            OmX[i] = s.Omega.X; OmY[i] = s.Omega.Y; OmZ[i] = s.Omega.Z;
            HX[i] = s.GyroH.X; HY[i] = s.GyroH.Y; HZ[i] = s.GyroH.Z;
            MainFuel[i] = s.MainFuel; RcsFuel[i] = s.RcsFuel; Throttle[i] = s.Throttle;
            Mode[i] = s.Mode; Dumping[i] = s.Dumping;
            Lat[i] = s.Latitude; Lon[i] = s.Longitude;
            SasOn[i] = s.SasOn; SasPhase[i] = s.SasPhase;
            SasQW[i] = s.SasTarget.W; SasQX[i] = s.SasTarget.X; SasQY[i] = s.SasTarget.Y; SasQZ[i] = s.SasTarget.Z;
            SasIX[i] = s.SasIntegral.X; SasIY[i] = s.SasIntegral.Y; SasIZ[i] = s.SasIntegral.Z;
        }
    }

    /// <summary>
    /// Batch stepping. Each craft is independent: it reads and writes only its own index, so the result does not depend on the order or on how
    /// the range is split between threads. A Job would call <see cref="StepRange"/> on its own slice.
    /// </summary>
    public static class FlightBatch
    {
        public static void StepRange(in FlightSoA soa, ReadOnlySpan<ControlInput> inputs, in FlightParams p, double time, double dt,
            int from, int to, Span<StepReport> reports)
        {
            for (int i = from; i < to; i++)
            {
                FlightState s = soa.Load(i);
                FlightKernel.Step(ref s, inputs[i], p, time, dt, out StepReport r);
                soa.Store(i, s);
                if (reports.Length > i) reports[i] = r;
            }
        }

        public static void StepAll(in FlightSoA soa, ReadOnlySpan<ControlInput> inputs, in FlightParams p, double time, double dt, Span<StepReport> reports)
        {
            StepRange(soa, inputs, p, time, dt, 0, soa.Length, reports);
        }
    }
}
