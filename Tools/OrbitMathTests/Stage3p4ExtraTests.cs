using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Net;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 3.4 executor's own tests for the engine-free helpers (the acceptance tests are Stage3p4Tests): edge cases of the body table
    /// lookups and the ghost distance that the acceptance suite does not spell out.
    /// </summary>
    public class Stage3p4ExtraTests
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

        [Test]
        public void GlobalPosition_OfTheRoot_IsTheLocalPointItself()
        {
            int sun = Id("sun");
            var local = new Vec3(1.0e9, -2.0e8, 3.0e7);
            Assert.That(Vec3.Length(Table.GlobalPosition(sun, 1.23e6, local) - local), Is.LessThan(1e-6));
        }

        [Test]
        public void ApplyTo_IdIsCaseSensitive_AndSwitchesTheBody()
        {
            var p = new FlightParams {Craft = FlightConfig.Default(), MaxLandingSpeed = 4.0};
            Assert.That(Table.ApplyTo("Phobos", ref p), Is.False, "the ids of the world file are lowercase");
            Assert.That(Table.ApplyTo("phobos", ref p), Is.True);
            double phobosNu = p.Nu;
            double phobosRadius = p.Surface.Radius;
            Assert.That(Table.ApplyTo("mars", ref p), Is.True, "the parameters follow the craft to the next body");
            Assert.That(p.Nu, Is.EqualTo(Table[Id("mars")].Nu));
            Assert.That(p.Surface.Radius, Is.EqualTo(Table[Id("mars")].Radius));
            Assert.That(p.Nu, Is.Not.EqualTo(phobosNu));
            Assert.That(p.Surface.Radius, Is.Not.EqualTo(phobosRadius));
            Assert.That(p.MaxLandingSpeed, Is.EqualTo(4.0));
        }

        [Test]
        public void GlobalDistance_OfTheSameGlobalPoint_WrittenAroundTwoBodies_IsZero()
        {
            const double t = 5.5e5;
            int phobos = Id("phobos"), mars = Id("mars");
            var atPhobos = new Vec3(12_000.0, 4_000.0, -2_000.0);
            Vec3 global = Table.GlobalPosition(phobos, t, atPhobos);
            Table.GlobalState(mars, t, out Vec3 gm, out _);
            var atMars = global - gm;
            Assert.That(GhostView.GlobalDistance(Table, phobos, atPhobos, mars, atMars, t), Is.LessThan(1e-6),
                "one and the same global point in two frames has zero global distance");
        }

        [Test]
        public void GlobalDistance_NoTableAndUnknownParents_Behave()
        {
            // the function is pure and allocation-free; here the behavioural corners only (the -1/-1 pair is the no-table one-body fallback)
            var a = new Vec3(10, 0, 0);
            var b = new Vec3(20, 0, 0);
            Assert.That(GhostView.GlobalDistance(null, -1, a, -1, b, 0.0), Is.EqualTo(10.0).Within(1e-6));
            Assert.That(GhostView.GlobalDistance(Table, -1, a, -1, b, 0.0), Is.EqualTo(10.0).Within(1e-6), "-1/-1 is the one-body fallback with a table too");
            Assert.That(double.IsPositiveInfinity(GhostView.GlobalDistance(null, 0, a, -1, b, 0.0)), Is.True);
        }
    }
}
