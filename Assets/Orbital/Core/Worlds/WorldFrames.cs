using System.Collections.Generic;
using Orbital.Core.Flight;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Worlds
{
    /// <summary>
    /// The <see cref="BodyFrameTable"/> of the shared world file (stage 3.1): the same numbers the server reads from the same file
    /// (OrbitServer/World.cs), so both sides apply the sphere of influence rule to the same bodies. Engine-free, client only (the server builds the
    /// table from its own world definition).
    /// </summary>
    public static class WorldFrames
    {
        public static BodyFrameTable Build(WorldFileDef def, double minRadiusFactor = TrajectoryUtility.DefaultMinRadiusFactor)
        {
            if (def?.Bodies == null || def.Bodies.Count == 0) throw new WorldFileException("the world file has no bodies");
            Dictionary<string, int> index = new Dictionary<string, int>();
            List<BodyFrameSource> sources = new List<BodyFrameSource>(def.Bodies.Count);
            foreach (WorldBodyDef d in def.Bodies)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Id)) throw new WorldFileException("a body has no id");
                BodyFrameSource s = new BodyFrameSource
                {
                    Id = d.Id, Parent = -1, Nu = d.Nu, Radius = d.Radius, Virtual = d.Virtual,
                    SoiOverride = d.SoiRadius ?? double.NaN, OrbitNu = double.NaN,
                };
                if (d.Parent != null)
                {
                    if (!index.TryGetValue(d.Parent, out s.Parent)) throw new WorldFileException($"the parent {d.Parent} of {d.Id} must come before it");
                    WorldOrbitDef o = d.Orbit ?? throw new WorldFileException($"body {d.Id} has no orbit");
                    s.InclinationDeg = o.InclinationDeg;
                    s.Eccentricity = o.Eccentricity;
                    s.SemiMajorAxis = o.SemiMajorAxis;
                    s.LanDeg = o.LanDeg;
                    s.AopDeg = o.AopDeg;
                    s.MeanAnomalyDeg = o.MeanAnomalyDeg;
                    s.Epoch = o.Epoch;
                    s.OrbitNu = o.OrbitNu ?? double.NaN;
                }
                if (d.Rotation != null)
                {
                    s.Rotation = new RotationParams
                    {
                        PeriodSeconds = d.Rotation.PeriodSeconds, ObliquityDegrees = d.Rotation.ObliquityDeg,
                        AxisAzimuthDegrees = d.Rotation.AxisAzimuthDeg, InitialAngleDegrees = d.Rotation.InitialAngleDeg,
                        TidallyLocked = d.Rotation.TidallyLocked,
                    };
                }
                index[d.Id] = sources.Count;
                sources.Add(s);
            }
            return BodyFrameTable.Build(sources, def.EpochZero, minRadiusFactor);
        }
    }
}
