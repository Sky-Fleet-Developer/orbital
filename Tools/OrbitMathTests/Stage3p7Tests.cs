using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Orbital.Core.Config;
using Orbital.Core.Flight;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>Stages 3.4b and 3.7: the forecast of a sphere of influence change, start points in space, per-point craft, shared landing speed.</summary>
    public class Stage3p7Tests
    {
        private static string ConfigDir([CallerFilePath] string file = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", "..", "Assets", "_game", "Config"));

        private static BodyFrameTable _table;
        private static BodyFrameTable Table => _table ??= WorldFrames.Build(WorldFileLoader.Parse(File.ReadAllText(Path.Combine(ConfigDir(), WorldFileHash.FileName))));
        private static GameConfig Config => GameConfigLoader.FromDirectory(ConfigDir());

        // Coasts by the same Kepler conic until the one SOI rule changes the body; returns the time of the change.
        private static double RuleTime(int parent, Vec3 pos, Vec3 vel, double t0, double limit, double step, out int to)
        {
            StaticOrbit orbit = new StaticOrbit {Nu = Table[parent].Nu};
            orbit.Calculate(new Ara3D.DVector3(pos.X, pos.Y, pos.Z), new Ara3D.DVector3(vel.X, vel.Y, vel.Z), t0);
            for (double t = t0 + step; t < t0 + limit; t += step)
            {
                var (p, v) = orbit.GetSample(t);
                Vec3 sp = new Vec3(p.x, p.y, p.z), sv = new Vec3(v.x, v.y, v.z);
                int now = parent;
                if (SoiRule.Check(Table, ref now, t, ref sp, ref sv))
                {
                    to = now;
                    return t;
                }
            }
            to = -1;
            return double.NaN;
        }

        [Test]
        public void Forecast_LeavingPhobos_MatchesTheRule()
        {
            int phobos = Table.IndexOf("phobos");
            double r = Table[phobos].Radius + 500.0;
            Vec3 pos = new Vec3(r, 0, 0), vel = new Vec3(25, 8, 0);
            Assert.That(SoiForecast.TryNext(Table, phobos, pos, vel, 1000.0, out double when, out int to), Is.True);
            Assert.That(to, Is.EqualTo(Table[phobos].Parent));
            double rule = RuleTime(phobos, pos, vel, 1000.0, 5000.0, 0.05, out int ruleTo);
            Assert.That(ruleTo, Is.EqualTo(to));
            Assert.That(when, Is.EqualTo(rule).Within(0.1));
        }

        [Test]
        public void Forecast_EarthToMoon_FindsTheEntry()
        {
            int earth = Table.IndexOf("earth"), moon = Table.IndexOf("moon");
            double t0 = 1000.0;
            // aim a fast hyperbola-free transfer at where the Moon will be: search the start time so the craft meets it
            Table.LocalState(moon, t0 + 40000.0, out Vec3 target, out _);
            double speed = (Vec3.Length(target) - 7e6) / 40000.0;
            Vec3 dir = Vec3.Normalize(target);
            Vec3 side = Vec3.Normalize(Vec3.Cross(dir, new Vec3(0.3, 0.5, 0.8)));
            Vec3 pos = dir * 7e6 + side * 2e5, vel = dir * (Math.Sqrt(2.0 * Table[earth].Nu / 7e6) * 1.02) + side * 10.0;
            Assert.That(speed, Is.GreaterThan(0));
            bool found = SoiForecast.TryNext(Table, earth, pos, vel, t0, out double when, out int to);
            double rule = RuleTime(earth, pos, vel, t0, 5e6, 20.0, out int ruleTo);
            Assert.That(found, Is.True);
            Assert.That(to, Is.EqualTo(ruleTo), "the forecast names the body the rule changes to");
            Assert.That(when, Is.EqualTo(rule).Within(40.0));
        }

        [Test]
        public void Forecast_NothingOnALowCircularOrbit_NorOnAFallIntoTheGround()
        {
            int earth = Table.IndexOf("earth");
            double r = Table[earth].Radius + 400000.0;
            double circular = Math.Sqrt(Table[earth].Nu / r);
            Assert.That(SoiForecast.TryNext(Table, earth, new Vec3(r, 0, 0), new Vec3(0, circular, 0), 0.0, out _, out _), Is.False);
            int phobos = Table.IndexOf("phobos");
            double rp = Table[phobos].Radius + 5000.0;
            Assert.That(SoiForecast.TryNext(Table, phobos, new Vec3(rp, 0, 0), new Vec3(-30, 3, 0), 0.0, out _, out _), Is.False, "falling, not leaving");
            Assert.That(SoiForecast.TryNext(Table, phobos, new Vec3(rp, 0, 0), new Vec3(30, 0, 0), 0.0, out _, out _), Is.False, "radial: unsupported, no answer");
        }

        [Test]
        public void StartPoints_AllAreValid_SpacePointHasAnOrbitInsideTheSphere()
        {
            GameConfig config = Config;
            StartPointDef orbit = null;
            foreach (StartPointDef point in config.StartPoints.StartPoints)
            {
                int body = Table.IndexOf(point.BodyId);
                Assert.That(body, Is.GreaterThanOrEqualTo(0), point.Id);
                Assert.That(Table[body].Radius, Is.GreaterThan(0), point.Id);
                if (point.CraftId != null) Assert.That(config.FindCraft(point.CraftId), Is.Not.Null, point.Id);
                if (!point.IsSpace) continue;
                orbit = point;
                Assert.That(point.AltitudeMeters, Is.GreaterThan(0), point.Id);
                Assert.That(Table[body].Radius + point.AltitudeMeters, Is.LessThan(Table[body].SoiRadius), point.Id);
            }
            Assert.That(orbit, Is.Not.Null, "stage 3.7 needs a start in orbit");
            Assert.That(config.FindStartPoint(null).IsSurface, Is.True, "the default start stays on the ground");
        }

        [Test]
        public void TestCraft_CanLiftOffFromEarth_AndHasTheDeltaVToLeaveIt()
        {
            GameConfig config = Config;
            StartPointDef orbit = config.StartPoints.StartPoints.Find(p => p.IsSpace);
            FlightConfig test = config.ResolveFlight(orbit.CraftId);
            FlightConfig standard = config.ResolveFlight(null);
            double deltaV(FlightConfig f) => f.Engine.Isp * 9.80665 * Math.Log((f.DryMass + f.MainFuelCapacity) / f.DryMass);
            Assert.That(deltaV(test), Is.GreaterThan(deltaV(standard) + 2000.0));
            Assert.That(deltaV(test), Is.GreaterThan(12000.0), "surface to orbit, then to the Moon and back");
            Assert.That(test.Engine.MaxThrust / ((test.DryMass + test.MainFuelCapacity) * 9.81), Is.GreaterThan(1.2), "lifts off from the Earth");
        }

        [Test]
        public void MaxLandingSpeed_ComesFromTheSharedFlightConfig()
        {
            Assert.That(Config.MaxLandingSpeed, Is.EqualTo(5.0));
            Assert.That(GameConfigLoader.FromJson("{ \"maxLandingSpeed\": 7.5 }", null, null, null).MaxLandingSpeed, Is.EqualTo(7.5));
            Assert.That(new GameConfig().MaxLandingSpeed, Is.EqualTo(5.0), "without the file: the same default as the server");
        }
    }
}
