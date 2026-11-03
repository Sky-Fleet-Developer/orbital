using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Net;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 3.4 acceptance tests (written by the coordinator against the approved design; the executor must not edit them):
    /// the engine-free helpers the Unity client uses to follow the craft's body: global positions, body parameters by id, ghost distances.
    /// </summary>
    public class Stage3p4Tests
    {
        private static string RepoDir([CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", ".."));

        private static BodyFrameTable _table;
        private static BodyFrameTable Table => _table ??= WorldFrames.Build(WorldFileLoader.Parse(
            File.ReadAllText(Path.Combine(RepoDir(), "Assets", "_game", "Config", WorldFileHash.FileName))));

        private static int Id(string id)
        {
            int i = Table.IndexOf(id);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), id);
            return i;
        }

        private static Vec3 ToRoot(int from, double t, Vec3 pos)
        {
            Vec3 vel = Vec3.Zero;
            Table.Convert(from, Id("sun"), t, ref pos, ref vel);
            return pos;
        }

        [Test]
        public void GlobalPosition_IsTheBodyStatePlusTheLocalPoint_AndAgreesWithConvert()
        {
            const double t = 3.21e6;
            var local = new Vec3(12_000.0, -3_500.0, 800.0);
            foreach (string id in new[] {"phobos", "mars", "moon", "earth", "charon", "sun"})
            {
                int i = Id(id);
                Table.GlobalState(i, t, out Vec3 body, out _);
                Vec3 g = Table.GlobalPosition(i, t, local);
                Assert.That(Vec3.Length(g - (body + local)), Is.LessThan(1e-6), id + ": body + local");
                Assert.That(Vec3.Length(g - ToRoot(i, t, local)), Is.LessThan(1e-6), id + ": the same as Convert to the root");
            }
        }

        [Test]
        public void ApplyTo_ById_SetsTheBody_AndKeepsTheCraft()
        {
            var p = new FlightParams {Craft = FlightConfig.Default(), MaxLandingSpeed = 7.5, Nu = 1.0};
            p.Craft.CgHeight = 1.234;
            Assert.That(Table.ApplyTo("phobos", ref p), Is.True);
            var q = new FlightParams {Craft = p.Craft, MaxLandingSpeed = 7.5, Nu = 1.0};
            Table.ApplyTo(Id("phobos"), ref q);
            Assert.That(p.Nu, Is.EqualTo(q.Nu), "the same as by index");
            Assert.That(p.Nu, Is.EqualTo(Table[Id("phobos")].Nu));
            Assert.That(p.Surface.Radius, Is.EqualTo(Table[Id("phobos")].Radius));
            Assert.That(p.MaxLandingSpeed, Is.EqualTo(7.5), "the landing limit stays");
            Assert.That(p.Craft.CgHeight, Is.EqualTo(1.234), "the craft stays");

            FlightParams before = p;
            Assert.That(Table.ApplyTo("nowhere", ref p), Is.False);
            Assert.That(Table.ApplyTo(null, ref p), Is.False);
            Assert.That(p.Nu, Is.EqualTo(before.Nu), "unknown id: unchanged");
            Assert.That(p.Surface.Radius, Is.EqualTo(before.Surface.Radius));
        }

        [Test]
        public void GlobalDistance_AcrossBodies_IsTheDistanceOfTheGlobalPoints()
        {
            // a craft just outside the sphere of influence of Phobos (around Mars) and one inside it (around Phobos)
            const double t = 7.7e5;
            int mars = Id("mars"), phobos = Id("phobos");
            var inside = new Vec3(16_000.0, 0, 0);
            Table.GlobalState(phobos, t, out Vec3 ph, out _);
            Table.GlobalState(mars, t, out Vec3 ma, out _);
            Vec3 outsideLocal = ph - ma + new Vec3(17_500.0, 2_000.0, 0); // the same neighbourhood, written around Mars

            double d = GhostView.GlobalDistance(Table, phobos, inside, mars, outsideLocal, t);
            double expected = Vec3.Length(Table.GlobalPosition(phobos, t, inside) - Table.GlobalPosition(mars, t, outsideLocal));
            Assert.That(d, Is.EqualTo(expected).Within(1e-6));
            Assert.That(d, Is.LessThan(20_000.0), "neighbours across the border are within the 20 km of the 3D view");
            Assert.That(GhostView.GlobalDistance(Table, mars, outsideLocal, phobos, inside, t), Is.EqualTo(d).Within(1e-6), "symmetric");
        }

        [Test]
        public void GlobalDistance_SameBodyOrNoTable_FallsBack()
        {
            var a = new Vec3(1, 2, 3);
            var b = new Vec3(4, 6, 3);
            int moon = Id("moon");
            Assert.That(GhostView.GlobalDistance(Table, moon, a, moon, b, 0.0), Is.EqualTo(5.0).Within(1e-6), "same body");
            Assert.That(GhostView.GlobalDistance(null, -1, a, -1, b, 0.0), Is.EqualTo(5.0), "no table, one body");
            Assert.That(GhostView.GlobalDistance(null, 2, a, 2, b, 0.0), Is.EqualTo(5.0), "no table, the same parent");
            Assert.That(double.IsPositiveInfinity(GhostView.GlobalDistance(null, 2, a, 3, b, 0.0)), Is.True, "no table, different bodies");
            Assert.That(double.IsPositiveInfinity(GhostView.GlobalDistance(Table, -1, a, moon, b, 0.0)), Is.True, "unknown parent");
        }
    }
}
