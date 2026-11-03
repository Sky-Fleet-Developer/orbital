using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>Stage 3.1: the shared body table (Core/Flight/SoiFrames.cs) built from world.json and the one sphere of influence rule.</summary>
    public class Stage3SoiTests
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
        public void SoiRadii_FromTheWorldFile()
        {
            Assert.That(double.IsPositiveInfinity(Table[Id("sun")].SoiRadius));
            Assert.That(Table[Id("earth")].SoiRadius, Is.GreaterThan(9e8));
            ref readonly BodyFrame phobos = ref Table[Id("phobos")];
            Assert.That(phobos.SoiRadius, Is.EqualTo(phobos.Radius * TrajectoryUtility.DefaultMinRadiusFactor).Within(1e-6), "Phobos: the R*k minimum");
            // the members of the double system: the Laplace sphere against the barycenter's nu (the sum of the pair), as the server does
            int bary = Id("pluto-charon");
            foreach (string member in new[] {"pluto", "charon"})
            {
                ref readonly BodyFrame m = ref Table[Id(member)];
                Assert.That(m.Parent, Is.EqualTo(bary), member);
                double expected = TrajectoryUtility.GetSphereOfInfluenceRadius(m.Orbit.SemiMajorAxis, m.Nu, Table[bary].Nu, m.Radius);
                Assert.That(m.SoiRadius, Is.EqualTo(expected).Within(expected * 1e-12), member);
            }
        }

        [Test]
        public void Convert_ThereAndBack_IsExact_AndKeepsTheGlobalState()
        {
            int moon = Id("moon"), phobos = Id("phobos"), charon = Id("charon");
            const double t = 1.234e6;
            Vec3 p0 = new Vec3(1.9e6, -3e5, 7e5), v0 = new Vec3(10, 1650, -40);
            foreach ((int a, int b) in new[] {(moon, phobos), (phobos, charon), (moon, Id("earth")), (charon, Id("sun"))})
            {
                Vec3 p = p0, v = v0;
                Table.Convert(a, b, t, ref p, ref v);
                Table.GlobalState(a, t, out Vec3 ga, out Vec3 gva);
                Table.GlobalState(b, t, out Vec3 gb, out Vec3 gvb);
                double scale = Math.Max(1.0, Vec3.Length(ga) + Vec3.Length(gb));
                Assert.That(Vec3.Length(p + gb - (p0 + ga)), Is.LessThan(scale * 1e-14 + 1e-6), $"global position {a}->{b}");
                Assert.That(Vec3.Length(v + gvb - (v0 + gva)), Is.LessThan(1e-6), $"global velocity {a}->{b}");
                Table.Convert(b, a, t, ref p, ref v);
                Assert.That(Vec3.Length(p - p0), Is.LessThan(scale * 1e-14 + 1e-6), $"round trip {a}<->{b}");
                Assert.That(Vec3.Length(v - v0), Is.LessThan(1e-6), $"round trip velocity {a}<->{b}");
            }
        }

        [Test]
        public void Rule_EntersAndLeaves_KeepingTheGlobalState()
        {
            int earth = Id("earth"), moon = Id("moon");
            const double t = 5e5;
            Table.LocalState(moon, t, out Vec3 mp, out Vec3 mv);
            double soi = Table[moon].SoiRadius;
            // just inside the Moon's sphere, in the Earth frame: enters the Moon
            Vec3 pos = mp + new Vec3(soi * 0.99, 0, 0), vel = mv + new Vec3(0, 300, 0);
            Table.GlobalState(earth, t, out Vec3 ge, out Vec3 gve);
            Vec3 globalPos = pos + ge, globalVel = vel + gve;
            int parent = earth;
            Assert.That(SoiRule.Check(Table, ref parent, t, ref pos, ref vel), Is.True);
            Assert.That(parent, Is.EqualTo(moon));
            Assert.That(Vec3.Length(pos - new Vec3(soi * 0.99, 0, 0)), Is.LessThan(1e-3));
            Table.GlobalState(moon, t, out Vec3 gm, out Vec3 gvm);
            Assert.That(Vec3.Length(pos + gm - globalPos), Is.LessThan(1e-3));
            Assert.That(Vec3.Length(vel + gvm - globalVel), Is.LessThan(1e-9));
            // inside the band between 1.0 and 1.005: stays; outside: back to the Earth
            Vec3 band = new Vec3(soi * 1.003, 0, 0), v = Vec3.Zero;
            Assert.That(SoiRule.Check(Table, ref parent, t, ref band, ref v), Is.False);
            Assert.That(parent, Is.EqualTo(moon));
            Vec3 outside = new Vec3(soi * 1.006, 0, 0);
            Assert.That(SoiRule.Check(Table, ref parent, t, ref outside, ref v), Is.True);
            Assert.That(parent, Is.EqualTo(earth));
            Assert.That(Vec3.Length(outside - (mp + new Vec3(soi * 1.006, 0, 0))), Is.LessThan(1e-3));
        }

        [Test]
        public void Rule_NoChatterAlongTheBorder()
        {
            // 1000 points skimming the Moon's sphere radius with noise inside the hysteresis band: at most one change
            int earth = Id("earth"), moon = Id("moon");
            double soi = Table[moon].SoiRadius;
            int parent = earth, changes = 0;
            var rng = new Random(7);
            for (int k = 0; k < 1000; k++)
            {
                double t = 1e5 + k;
                Table.LocalState(moon, t, out Vec3 mp, out _);
                double r = k == 0 ? soi * 0.999 : soi * (1.0 + 0.004 * rng.NextDouble()); // the first point is inside, then the band
                double a = k * 1e-3;
                Vec3 rel = new Vec3(Math.Cos(a), Math.Sin(a), 0) * r;
                Vec3 pos = parent == moon ? rel : mp + rel, vel = Vec3.Zero;
                if (SoiRule.Check(Table, ref parent, t, ref pos, ref vel)) changes++;
            }
            Assert.That(changes, Is.LessThanOrEqualTo(1));
            Assert.That(parent, Is.EqualTo(moon));
        }

        [Test]
        public void Rule_NestedSpheres_EntersSeveralLevelsAtOnce()
        {
            // a point right next to Pluto in the Sun frame: Sun -> barycenter -> Pluto in one check
            int sun = Id("sun"), pluto = Id("pluto");
            const double t = 2e6;
            Table.GlobalState(pluto, t, out Vec3 gp, out Vec3 gv);
            Vec3 pos = gp + new Vec3(Table[pluto].Radius * 1.2, 0, 0), vel = gv;
            int parent = sun;
            Assert.That(SoiRule.Check(Table, ref parent, t, ref pos, ref vel), Is.True);
            Assert.That(Table.IdOf(parent), Is.EqualTo("pluto"));
            Assert.That(Vec3.Length(pos - new Vec3(Table[pluto].Radius * 1.2, 0, 0)), Is.LessThan(1e-2));
        }
    }
}
