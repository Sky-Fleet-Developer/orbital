using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.Bodies;
using Orbital.Core.Flight;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 2c.W2b (client side of the world zero epoch and of the model data from the world file): the client world built at epochZero has the
    /// planets where the server has them (orbit epoch = epochZero + orbit.epoch, rotation angle = Phase0 + Omega * (t - epochZero)); radius and
    /// rotation of a body come from the file, the body table gives the view facets only.
    /// </summary>
    public class Stage2cW2Tests
    {
        private const double Shift = 1000.0;
        private static readonly double[] Times = {0.0, 86400.0, 3.15576e7, -3.0e8};

        private static string WorldPath([CallerFilePath] string file = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", "..", "Assets", "_game", "Config", WorldFileHash.FileName));

        [Test]
        public void SharedFile_HasEpochZero_AndMissingFieldIsZero()
        {
            LoadedWorld world = WorldFileLoader.Load(File.ReadAllText(WorldPath()));
            Assert.AreEqual(0.0, world.EpochZero);
            Assert.AreEqual(0.0, WorldFileLoader.Parse("{\"bodies\": []}").EpochZero);
            Assert.AreEqual(1234.5, WorldFileLoader.Parse("{\"epochZero\": 1234.5, \"bodies\": []}").EpochZero);
        }

        [Test]
        public void WorldAtEpochZero_IsTheZeroWorld_ShiftedInTime()
        {
            string json = File.ReadAllText(WorldPath());
            LoadedWorld zero = WorldFileLoader.Build(WorldFileLoader.Parse(json));
            WorldFileDef shiftedDef = WorldFileLoader.Parse(json);
            shiftedDef.EpochZero = Shift;
            LoadedWorld shifted = WorldFileLoader.Build(shiftedDef);
            Assert.AreEqual(Shift, shifted.EpochZero);

            Dictionary<string, IMassSystem> a = ById(zero), b = ById(shifted);
            foreach (double t in Times)
            {
                Dictionary<IMassSystem, DVector3> pa = WorldFileTests.GlobalPositions(zero.Root, t, zero.EpochZero);
                Dictionary<IMassSystem, DVector3> pb = WorldFileTests.GlobalPositions(shifted.Root, t + Shift, shifted.EpochZero);
                foreach (string id in a.Keys)
                {
                    double scale = Math.Max(pa[a[id]].Length(), 1.0);
                    double d = (pa[a[id]] - pb[b[id]]).Length();
                    Assert.That(d, Is.LessThanOrEqualTo(1e-9 * scale), $"{id} at t = {t}: {d} m of {scale} m");
                }
            }
        }

        [Test]
        public void SurfaceAngle_CountsFromEpochZero()
        {
            RotationParams rot = new RotationParams {PeriodSeconds = 86164.1, ObliquityDegrees = 23.44, InitialAngleDegrees = 40};
            SurfaceData zero = SurfaceBuilder.Build(6.371e6, rot, default);
            SurfaceData shifted = SurfaceBuilder.Build(6.371e6, rot, default, Shift);
            Assert.AreEqual(Shift, shifted.EpochZero);
            Assert.AreEqual(Surface.Angle(zero, 0.0), Surface.Angle(shifted, Shift), 1e-15);
            Assert.AreEqual(Surface.Angle(zero, 12345.6), Surface.Angle(shifted, 12345.6 + Shift), 1e-12);
            // the server formula: Phase0 + (Omega * (t - EpochZero)) mod 2 pi
            double t = 3e8;
            Assert.AreEqual(shifted.Phase0 + (shifted.Omega * (t - Shift)) % (2 * Math.PI), Surface.Angle(shifted, t), 1e-12);
        }

        [Test]
        public void TidalLockPhase_IsTakenAtEpochZero()
        {
            RotationParams rot = new RotationParams {TidallyLocked = true};
            OrbitFrame frame = new OrbitFrame
            {
                HasParent = true, Forward = Vec3.UnitX, Normal = Vec3.UnitY, Right = Vec3.Cross(Vec3.UnitY, Vec3.UnitX), MeanMotion = 1e-5,
                PositionAtEpochZero = new Vec3(0, 0, 4e8),
            };
            SurfaceData s = SurfaceBuilder.Build(1.7e6, rot, frame, Shift);
            // at epochZero the prime meridian faces the parent: the direction to the parent is -position
            Vec3 toParent = Vec3.Normalize(-frame.PositionAtEpochZero);
            double angle = Surface.Angle(s, Shift);
            Vec3 meridian = s.EquatorX * Math.Cos(angle) + s.EquatorY * Math.Sin(angle);
            Assert.AreEqual(1.0, Vec3.Dot(meridian, toParent), 1e-12);
        }

        [Test]
        public void Record_TakesModelFacetsFromTheFile_AndViewFacetsFromTheTable()
        {
            ViewFacet look = new ViewFacet();
            BodyRecord table = new BodyRecord
            {
                name = "Earth[3]",
                facets = new List<IBodyFacet> {new PhysicalFacet {radiusMeters = 1.0}, new RotationFacet {rotationPeriodSeconds = 7.0}, look},
            };
            table.Rebuild();
            WorldBodyDef def = new WorldBodyDef
            {
                Id = "earth", Name = "Earth", Nu = 3.986e14, Radius = 6.371e6,
                Rotation = new WorldRotationDef {PeriodSeconds = 86164.1, ObliquityDeg = 23.44, AxisAzimuthDeg = 5, InitialAngleDeg = 40},
            };

            BodyRecord record = WorldBodyRecords.Make(def, table);
            Assert.AreEqual("Earth[3]", record.name);
            Assert.AreEqual(6.371e6, record.GetFacet<PhysicalFacet>().radiusMeters);
            RotationFacet rotation = record.GetFacet<RotationFacet>();
            Assert.AreEqual(86164.1, rotation.rotationPeriodSeconds);
            Assert.AreEqual(23.44, rotation.axialTiltDegrees);
            Assert.AreEqual(5.0, rotation.axisAzimuthDegrees);
            Assert.AreEqual(40.0, rotation.initialAngleDegrees);
            Assert.AreSame(look, record.GetFacet<ViewFacet>(), "view facets come from the table");
            Assert.AreEqual(3, record.facets.Count);
            Assert.AreEqual(1.0, table.GetFacet<PhysicalFacet>().radiusMeters, "the table record is not changed");
            Assert.AreEqual(7.0, table.GetFacet<RotationFacet>().rotationPeriodSeconds);

            BodyRecord noTable = WorldBodyRecords.Make(def, null);
            Assert.AreEqual("Earth", noTable.name);
            Assert.AreEqual(6.371e6, noTable.GetFacet<PhysicalFacet>().radiusMeters, "a body without a table row still has its radius");

            WorldBodyDef still = new WorldBodyDef {Id = "rock", Nu = 1, Radius = 10, Rotation = new WorldRotationDef()};
            Assert.IsFalse(WorldBodyRecords.Make(still, table).HasFacet<RotationFacet>(), "period 0 and not locked: no spin, not the table's");
            WorldBodyDef barycenter = new WorldBodyDef {Id = "bc", Virtual = true, Radius = 0};
            BodyRecord virtualRecord = WorldBodyRecords.Make(barycenter, table);
            Assert.IsFalse(virtualRecord.HasFacet<PhysicalFacet>());
            Assert.IsFalse(virtualRecord.HasFacet<RotationFacet>());
        }

        [Test]
        public void SharedFile_EveryBodyWithASurface_GetsItsRadius()
        {
            LoadedWorld world = WorldFileLoader.Load(File.ReadAllText(WorldPath()));
            foreach (WorldBodyDef body in world.Definition.Bodies.Where(b => !b.Virtual))
            {
                BodyRecord record = WorldBodyRecords.Make(body, null);
                Assert.That(record.GetFacet<PhysicalFacet>()?.radiusMeters, Is.EqualTo(body.Radius).And.GreaterThan(0), body.Id);
            }
        }

        private static Dictionary<string, IMassSystem> ById(LoadedWorld world)
        {
            Dictionary<string, IMassSystem> result = new Dictionary<string, IMassSystem>();
            foreach (KeyValuePair<IMassSystem, WorldBodyDef> pair in world.BodyOf)
            {
                if (pair.Key == world.Root) continue;
                if (!result.ContainsKey(pair.Value.Id) || !(pair.Key is CelestialBody)) result[pair.Value.Id] = pair.Key;
            }

            return result;
        }

        private class ViewFacet : IBodyFacet
        {
            public const int TypeId = 9;
            public int Id => TypeId;
        }
    }
}
