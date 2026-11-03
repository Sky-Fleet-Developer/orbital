using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>Stage 3.5: the engine-free projection of a world body into scaled space (Core/Flight/CelestialProjection.cs).</summary>
    public class Stage3ViewTests
    {
        private const double L = CelestialProjection.DefaultScaledDistance;

        private static ProjectionCamera Camera(Vec3 eye, double fovDeg, int widthPx) =>
            new ProjectionCamera {Eye = eye, HorizontalFovRad = fovDeg * Math.PI / 180.0, WidthPx = widthPx};

        private static ProjectedBody Project(ProjectionCamera cam, Vec3 bodyCenter, double bodyRadius) =>
            CelestialProjection.Project(in cam, bodyCenter, bodyRadius, L, CelestialProjection.DefaultMinPixels);

        private static void AssertFinite(double v, string name)
        {
            Assert.That(double.IsNaN(v), Is.False, name + " is NaN");
            Assert.That(double.IsInfinity(v), Is.False, name + " is infinite");
        }

        private static void AssertFinite(ProjectedBody b, string name)
        {
            AssertFinite(b.Direction.X, name + ".Direction.X");
            AssertFinite(b.Direction.Y, name + ".Direction.Y");
            AssertFinite(b.Direction.Z, name + ".Direction.Z");
            AssertFinite(b.Distance, name + ".Distance");
            AssertFinite(b.AngularRadius, name + ".AngularRadius");
            AssertFinite(b.ScaledPosition.X, name + ".ScaledPosition.X");
            AssertFinite(b.ScaledPosition.Y, name + ".ScaledPosition.Y");
            AssertFinite(b.ScaledPosition.Z, name + ".ScaledPosition.Z");
            AssertFinite(b.ScaledRadius, name + ".ScaledRadius");
        }

        [Test]
        public void Projection_KeepsTheAngularSize_Exactly()
        {
            double[] radii = {1e3, 6.371e6, 3.3895e6, 6.96e8, 7e8};
            var cam = Camera(Vec3.Zero, 60.0, 1920);
            foreach (double r in radii)
            {
                double[] distances = {r * 1.01, r * 2.0, r * 1e2, r * 1e4, 1e13};
                foreach (double d in distances)
                {
                    if (d <= r) continue;
                    ProjectedBody b = Project(cam, new Vec3(d, 0, 0), r);
                    Assert.That(Math.Asin(b.ScaledRadius / L), Is.EqualTo(Math.Asin(r / d)).Within(1e-9), $"R = {r}, d = {d}");
                    AssertFinite(b, $"R = {r}, d = {d}");
                }
            }
        }

        [Test]
        public void Projection_EarthAtOneAuAndSunAtFortyAu_AreFiniteAndFloatSafe()
        {
            var cam = Camera(Vec3.Zero, 60.0, 1920);
            Vec3 dir = new Vec3(2.0 / 3.0, 2.0 / 3.0, 1.0 / 3.0); // unit, no zero components
            (string name, double r, double d)[] cases =
            {
                ("Earth at 1 AU", 6.371e6, 1.496e11),
                ("Sun at 40 AU", 6.96e8, 40.0 * 1.496e11)
            };
            foreach ((string name, double r, double d) in cases)
            {
                ProjectedBody b = Project(cam, dir * d, r);
                AssertFinite(b, name);
                Assert.That(b.Mode, Is.EqualTo(BodyDrawMode.Point), name);
                foreach (double s in new[] {b.ScaledPosition.X, b.ScaledPosition.Y, b.ScaledPosition.Z})
                {
                    float f = (float)s;
                    Assert.That(float.IsFinite(f), Is.True, name + " float ScaledPosition is finite");
                    Assert.That(f, Is.Not.EqualTo(0.0f), name + " float ScaledPosition is not zero");
                }
                float sr = (float)b.ScaledRadius;
                Assert.That(float.IsFinite(sr), Is.True, name + " float ScaledRadius is finite");
                Assert.That(sr, Is.Not.EqualTo(0.0f), name + " float ScaledRadius is not zero");
            }
        }

        [Test]
        public void Projection_PointOrDisk_IsDecidedByTheCameraPixels_NotByTheDistance()
        {
            const double marsR = 3.3895e6;
            const double d = 7.8e10;
            Vec3 mars = new Vec3(d, 0, 0);
            ProjectedBody flight = Project(Camera(Vec3.Zero, 60.0, 1920), mars, marsR);
            ProjectedBody telescope = Project(Camera(Vec3.Zero, 0.05, 512), mars, marsR);
            Assert.That(flight.Mode, Is.EqualTo(BodyDrawMode.Point), "the flight camera (60 degrees, 1920 px) sees a point");
            Assert.That(telescope.Mode, Is.EqualTo(BodyDrawMode.Disk), "the telescope (0.05 degrees, 512 px) sees a disk");
            // the geometry does not depend on the camera, only the mode does
            Assert.That(flight.AngularRadius, Is.EqualTo(telescope.AngularRadius).Within(1e-15));
            Assert.That(flight.ScaledRadius, Is.EqualTo(telescope.ScaledRadius).Within(1e-12));
        }

        [Test]
        public void Projection_DirectionIsUnit_AndScaledPositionIsDirectionTimesTheScaledDistance()
        {
            var cam = Camera(new Vec3(1e9, -2e9, 3e9), 60.0, 1920);
            (Vec3 center, double r)[] cases =
            {
                (new Vec3(1.5e9, -2e9, 3e9), 6.371e6), // the eye outside
                (cam.Eye, 6.371e6),                    // the eye at the center
                (new Vec3(1e9, -2e9, 3e9 + 1e7), 0.0)  // a zero radius body
            };
            foreach ((Vec3 center, double r) in cases)
            {
                ProjectedBody b = Project(cam, center, r);
                Assert.That(Math.Abs(Vec3.Length(b.Direction) - 1.0), Is.LessThan(1e-12), $"Direction unit for R = {r}");
                Assert.That(Vec3.Length(b.ScaledPosition - b.Direction * L), Is.LessThan(L * 1e-12), $"ScaledPosition for R = {r}");
                AssertFinite(b, $"R = {r}");
            }
        }

        [Test]
        public void Projection_EyeInsideTheBody_IsADiskOfTheScaledDistanceRadius()
        {
            var cam = Camera(Vec3.Zero, 60.0, 1920);
            ProjectedBody b = Project(cam, new Vec3(1e3, 0, 0), 1e6);
            Assert.That(b.Inside, Is.True);
            Assert.That(b.Distance, Is.EqualTo(1e3).Within(1e-9));
            Assert.That(b.AngularRadius, Is.EqualTo(Math.PI / 2.0).Within(1e-15));
            Assert.That(b.ScaledRadius, Is.EqualTo(L).Within(1e-9));
            Assert.That(b.Mode, Is.EqualTo(BodyDrawMode.Disk));
            Assert.That(Vec3.Length(b.ScaledPosition - b.Direction * L), Is.LessThan(L * 1e-12));
            AssertFinite(b, "inside the body");
        }

        [Test]
        public void Projection_EyeAtTheCenterOfTheBody_TakesAFixedDirection()
        {
            var cam = Camera(new Vec3(1e9, -2e9, 3e9), 60.0, 1920);
            ProjectedBody b = Project(cam, cam.Eye, 6.371e6);
            Assert.That(b.Distance, Is.EqualTo(0.0));
            Assert.That(b.Inside, Is.True);
            Assert.That(b.AngularRadius, Is.EqualTo(Math.PI / 2.0).Within(1e-15));
            Assert.That(b.ScaledRadius, Is.EqualTo(L).Within(1e-9));
            Assert.That(b.Mode, Is.EqualTo(BodyDrawMode.Disk));
            Assert.That(Vec3.Length(b.Direction - Vec3.UnitY), Is.LessThan(1e-15), "any fixed unit direction is fine, this pins UnitY");
            Assert.That(Vec3.Length(b.ScaledPosition - Vec3.UnitY * L), Is.LessThan(1e-9));
            AssertFinite(b, "at the center");
        }

        [Test]
        public void Projection_ZeroRadiusBody_IsAPointOfZeroRadius()
        {
            var cam = Camera(Vec3.Zero, 60.0, 1920);
            ProjectedBody b = Project(cam, new Vec3(1e9, 0, 0), 0.0);
            Assert.That(b.Mode, Is.EqualTo(BodyDrawMode.Point));
            Assert.That(b.AngularRadius, Is.EqualTo(0.0));
            Assert.That(b.ScaledRadius, Is.EqualTo(0.0));
            Assert.That(b.Inside, Is.False);
            Assert.That(Math.Abs(Vec3.Length(b.Direction) - 1.0), Is.LessThan(1e-12));
            AssertFinite(b, "zero radius");

            ProjectedBody atEye = Project(cam, cam.Eye, 0.0);
            Assert.That(atEye.Mode, Is.EqualTo(BodyDrawMode.Point));
            Assert.That(atEye.AngularRadius, Is.EqualTo(0.0));
            Assert.That(atEye.ScaledRadius, Is.EqualTo(0.0));
            Assert.That(atEye.Inside, Is.False);
            Assert.That(Vec3.Length(atEye.Direction - Vec3.UnitY), Is.LessThan(1e-15));
            AssertFinite(atEye, "zero radius at the eye");
        }
    }

    /// <summary>
    /// Stage 3.5b acceptance tests (written by the coordinator against the approved design; the executor must not edit them):
    /// flux of reflecting and emitting bodies, the Lambert phase, the camera exposure, the light direction and the empty star field.
    /// Flux unit: 1 = the Sun at 1 AU.
    /// </summary>
    public class Stage3LightTests
    {
        private const double Au = 1.495978707e11;
        private const double MoonR = 1.7374e6;
        private const double EarthMoon = 3.844e8;
        private const double SunR = 6.957e8;

        private static void AssertFinite(double v, string name)
        {
            Assert.That(double.IsNaN(v) || double.IsInfinity(v), Is.False, name + " = " + v);
        }

        private static double Full(double r, double dSun, double dEye, double albedo) =>
            albedo * (r / dSun) * (r / dSun) * (Au / dEye) * (Au / dEye);

        [Test]
        public void AstronomicalUnit_IsTheIauValue()
        {
            Assert.That(BodyFlux.AstronomicalUnit, Is.EqualTo(Au));
        }

        [Test]
        public void LambertPhase_HasTheKnownValues_AndIsMonotonic()
        {
            Assert.That(BodyFlux.LambertPhase(0.0), Is.EqualTo(1.0).Within(1e-15));
            Assert.That(BodyFlux.LambertPhase(Math.PI / 2), Is.EqualTo(1.0 / Math.PI).Within(1e-15));
            Assert.That(BodyFlux.LambertPhase(Math.PI), Is.EqualTo(0.0).Within(1e-15));
            Assert.That(BodyFlux.LambertPhase(-0.1), Is.EqualTo(1.0).Within(1e-15), "clamped to [0, pi]");
            Assert.That(BodyFlux.LambertPhase(4.0), Is.EqualTo(0.0).Within(1e-15), "clamped to [0, pi]");
            double prev = 1.0;
            for (int i = 1; i <= 1000; i++)
            {
                double f = BodyFlux.LambertPhase(Math.PI * i / 1000.0);
                Assert.That(f, Is.LessThanOrEqualTo(prev), "monotonic at step " + i);
                Assert.That(f, Is.GreaterThanOrEqualTo(0.0));
                prev = f;
            }
        }

        [Test]
        public void ReflectedFlux_FullMoonFromEarth_MatchesTheFormula()
        {
            // the Sun at the origin, the eye at 1 AU, the Moon behind the eye: phase angle 0
            var sun = Vec3.Zero;
            var eye = new Vec3(Au, 0, 0);
            var moon = new Vec3(Au + EarthMoon, 0, 0);
            double f = BodyFlux.ReflectedFlux(moon, MoonR, sun, eye, 0.12);
            double expected = Full(MoonR, Au + EarthMoon, EarthMoon, 0.12);
            Assert.That(f, Is.EqualTo(expected).Within(expected * 1e-12));
            Assert.That(f, Is.EqualTo(2.5e-6).Within(0.5e-6), "order of magnitude of the full Moon");
        }

        [Test]
        public void ReflectedFlux_AtQuadrature_IsTheFullFluxTimesOneOverPi()
        {
            var sun = Vec3.Zero;
            var moon = new Vec3(Au, 0, 0);
            var eye = new Vec3(Au, EarthMoon, 0); // the Sun and the eye at 90 degrees seen from the body
            double f = BodyFlux.ReflectedFlux(moon, MoonR, sun, eye, 0.12);
            double expected = Full(MoonR, Au, EarthMoon, 0.12) / Math.PI;
            Assert.That(f, Is.EqualTo(expected).Within(expected * 1e-9));
            Assert.That(f, Is.EqualTo(BodyFlux.ReflectedFlux(MoonR, Au, EarthMoon, 0.12, Math.PI / 2)).Within(expected * 1e-9),
                "the position overload agrees with the scalar one");
        }

        [Test]
        public void ReflectedFlux_NewMoon_IsZero_AndInverseSquareInTheEyeDistance()
        {
            var sun = Vec3.Zero;
            var moon = new Vec3(Au, 0, 0);
            double fullMoon = BodyFlux.ReflectedFlux(moon, MoonR, sun, new Vec3(Au - EarthMoon, 0, 0), 0.12);
            double newMoon = BodyFlux.ReflectedFlux(moon, MoonR, sun, new Vec3(Au + EarthMoon, 0, 0), 0.12);
            Assert.That(newMoon, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(newMoon, Is.LessThan(fullMoon * 1e-12), "the body between the eye and the Sun shows its dark side");

            double near = BodyFlux.ReflectedFlux(MoonR, Au, EarthMoon, 0.12);
            double far = BodyFlux.ReflectedFlux(MoonR, Au, 2 * EarthMoon, 0.12);
            Assert.That(near / far, Is.EqualTo(4.0).Within(1e-12));
            Assert.That(BodyFlux.ReflectedFlux(MoonR, Au, EarthMoon, 0.24) / near, Is.EqualTo(2.0).Within(1e-12), "linear in albedo");
        }

        [Test]
        public void ReflectedFlux_DegenerateInputs_StayFinite()
        {
            var sun = Vec3.Zero;
            var body = new Vec3(Au, 0, 0);
            Assert.That(BodyFlux.ReflectedFlux(body, 0.0, sun, new Vec3(Au + 1e9, 0, 0), 0.3), Is.EqualTo(0.0), "zero radius");
            AssertFinite(BodyFlux.ReflectedFlux(body, MoonR, sun, body, 0.3), "eye at the center");
            AssertFinite(BodyFlux.ReflectedFlux(body, MoonR, sun, body + new Vec3(10, 0, 0), 0.3), "eye inside");
            AssertFinite(BodyFlux.ReflectedFlux(body, MoonR, body, new Vec3(Au + 1e9, 0, 0), 0.3), "sun at the center");
            Assert.That(BodyFlux.ReflectedFlux(body, MoonR, sun, body, 0.3), Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void SolarFlux_IsOneAtOneAu_InverseSquare_AndClamped()
        {
            Assert.That(BodyFlux.SolarFlux(Au, SunR), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(BodyFlux.SolarFlux(40 * Au, SunR), Is.EqualTo(1.0 / 1600).Within(1e-15));
            Assert.That(BodyFlux.SolarFlux(0.5 * Au, 0.0), Is.EqualTo(4.0).Within(1e-12), "no radius: still the inverse square");
            double photosphere = (Au / SunR) * (Au / SunR);
            Assert.That(BodyFlux.SolarFlux(0.0, SunR), Is.EqualTo(photosphere).Within(photosphere * 1e-12), "clamped at the radius");
            Assert.That(BodyFlux.SolarFlux(SunR / 2, SunR), Is.EqualTo(photosphere).Within(photosphere * 1e-12));
            AssertFinite(BodyFlux.SolarFlux(0.0, 0.0), "zero distance and radius");
        }

        [Test]
        public void SunLight_Direction_IsUnit_AndUnitYAtTheSun()
        {
            var d = SunLight.Direction(new Vec3(3e11, -4e11, 1e9), new Vec3(1e3, 2e3, -5e2));
            Assert.That(Math.Abs(Vec3.Length(d) - 1.0), Is.LessThan(1e-12));
            Assert.That(d.X, Is.GreaterThan(0));
            Assert.That(d.Y, Is.LessThan(0));
            var at = SunLight.Direction(new Vec3(1, 2, 3), new Vec3(1, 2, 3));
            Assert.That(Vec3.Length(at - Vec3.UnitY), Is.LessThan(1e-15));
        }

        [Test]
        public void Exposure_Default_IsConsistent_AndMapsTheAnchors()
        {
            var p = ExposureParams.Default;
            Assert.That(p.FluxFloor, Is.GreaterThan(0));
            Assert.That(p.FluxCeiling, Is.GreaterThan(p.FluxFloor));
            Assert.That(p.MinPointBrightness, Is.GreaterThan(0));
            Assert.That(p.MaxPointBrightness, Is.GreaterThanOrEqualTo(p.MinPointBrightness));

            Assert.That(Exposure.PointBrightness(0.0, p), Is.EqualTo(0.0));
            Assert.That(Exposure.PointBrightness(-1.0, p), Is.EqualTo(0.0));
            Assert.That(Exposure.PointBrightness(double.NaN, p), Is.EqualTo(0.0));
            Assert.That(Exposure.PointBrightness(p.FluxFloor * 1e-3, p), Is.EqualTo(p.MinPointBrightness).Within(1e-15), "below the floor: the minimum");
            Assert.That(Exposure.PointBrightness(p.FluxCeiling * 1e6, p), Is.EqualTo(p.MaxPointBrightness).Within(1e-15), "far above: the cap");
        }

        [Test]
        public void Exposure_IsLogarithmic_Monotonic_AndBounded()
        {
            var p = new ExposureParams {FluxFloor = 1e-16, FluxCeiling = 1e4, MinPointBrightness = 0.05, MaxPointBrightness = 1.0};
            Assert.That(Exposure.PointBrightness(1e-6, p), Is.EqualTo(0.5).Within(1e-12), "the geometric mean of the anchors is the middle");
            Assert.That(Exposure.PointBrightness(1e4, p), Is.EqualTo(1.0).Within(1e-12), "the ceiling is 1");
            Assert.That(Exposure.PointBrightness(1e-11, p), Is.EqualTo(0.25).Within(1e-12));

            double prev = 0.0;
            for (int i = 0; i <= 2600; i++)
            {
                double flux = Math.Pow(10, -20 + i * 0.01);
                double b = Exposure.PointBrightness(flux, p);
                Assert.That(b, Is.GreaterThanOrEqualTo(prev), "monotonic at flux " + flux);
                Assert.That(b, Is.InRange(p.MinPointBrightness, p.MaxPointBrightness));
                Assert.That(float.IsFinite((float)b), Is.True);
                prev = b;
            }

            var hdr = p;
            hdr.MaxPointBrightness = 8.0;
            Assert.That(Exposure.PointBrightness(1e5, hdr), Is.GreaterThan(1.0), "HDR: the cap above 1 lets the Sun go brighter");
            Assert.That(Exposure.PointBrightness(1e-6, hdr), Is.EqualTo(0.5).Within(1e-12), "and does not dim the planets");
        }

        [Test]
        public void Exposure_SolarSystemFromEarth_KeepsEveryBodyVisible_AndOrdered()
        {
            var p = ExposureParams.Default;
            var sun = Vec3.Zero;
            var eye = new Vec3(Au, 0, 0);
            double sunB = Exposure.PointBrightness(BodyFlux.SolarFlux(Au, SunR), p);
            double moonB = Exposure.PointBrightness(BodyFlux.ReflectedFlux(new Vec3(Au + EarthMoon, 0, 0), MoonR, sun, eye, 0.12), p);
            double jupiterB = Exposure.PointBrightness(BodyFlux.ReflectedFlux(new Vec3(5.2 * Au, 0, 0), 6.9911e7, sun, eye, 0.54), p);
            double plutoB = Exposure.PointBrightness(BodyFlux.ReflectedFlux(new Vec3(39.5 * Au, 0, 0), 1.1883e6, sun, eye, 0.5), p);
            Assert.That(sunB, Is.GreaterThan(moonB));
            Assert.That(moonB, Is.GreaterThan(jupiterB));
            Assert.That(jupiterB, Is.GreaterThan(plutoB));
            Assert.That(plutoB, Is.GreaterThanOrEqualTo(p.MinPointBrightness), "Pluto stays visible");
        }

        [Test]
        public void EmptyStarField_ReturnsNothing_AndLeavesTheBufferAlone()
        {
            var buffer = new StarPoint[4];
            buffer[0].Flux = 42;
            int n = EmptyStarField.Instance.Query(Vec3.UnitY, 0.5, 1e-12, buffer);
            Assert.That(n, Is.EqualTo(0));
            Assert.That(buffer[0].Flux, Is.EqualTo(42));
            IStarField field = EmptyStarField.Instance;
            Span<StarPoint> onStack = stackalloc StarPoint[2];
            Assert.That(field.Query(Vec3.UnitY, 0.5, 1e-12, onStack), Is.EqualTo(0));
        }
    }

    /// <summary>
    /// Stage 3.5c acceptance tests (written by the coordinator against the approved design; the executor must not edit them):
    /// the near planet mode with its hysteresis, the far plane of the Local camera and the projection of the whole body table.
    /// </summary>
    public class Stage3NearViewTests
    {
        private const double MaxFar = NearPlanetMode.DefaultMaxFar;
        private const double EarthR = 6.371e6;
        private const double MoonR = 1.7374e6;
        private const double SunR = 6.957e8;

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

        private static BodyViewParams[] View(bool withStar = true)
        {
            var v = new BodyViewParams[Table.Count];
            for (int i = 0; i < v.Length; i++) v[i].Albedo = 0.3;
            if (withStar) v[Id("sun")].Star = true;
            return v;
        }

        [Test]
        public void Thresholds_AreTwoRadii_LimitedByTheFarBudget()
        {
            Assert.That(NearPlanetMode.ExitAltitude(MoonR), Is.EqualTo(2 * MoonR).Within(1e-6), "the Moon: 2R fits in the far budget");
            Assert.That(NearPlanetMode.EnterAltitude(MoonR), Is.EqualTo(MoonR).Within(1e-6), "enter = half of exit");

            double earthExit = NearPlanetMode.ExitAltitude(EarthR);
            double hFar = -EarthR + Math.Sqrt(EarthR * EarthR + (MaxFar - EarthR) * (MaxFar - EarthR));
            Assert.That(earthExit, Is.EqualTo(hFar).Within(1e-3), "the Earth: limited by far = maxFar");
            Assert.That(earthExit, Is.InRange(9.5e5, 9.7e5));
            Assert.That(NearPlanetMode.LocalFar(earthExit, EarthR), Is.EqualTo(MaxFar).Within(1e-3), "at the exit far is exactly the budget");

            Assert.That(NearPlanetMode.ExitAltitude(SunR), Is.EqualTo(0.0), "the Sun is never a near planet");
            Assert.That(NearPlanetMode.EnterAltitude(SunR), Is.EqualTo(0.0));
            Assert.That(NearPlanetMode.ExitAltitude(0.0), Is.EqualTo(0.0), "no radius");
        }

        [Test]
        public void LocalFar_IsHorizonPlusRadius_Clamped()
        {
            double h = 1e5;
            Assert.That(NearPlanetMode.LocalFar(h, MoonR), Is.EqualTo(Math.Sqrt(h * (2 * MoonR + h)) + MoonR).Within(1e-6));
            Assert.That(NearPlanetMode.LocalFar(0.0, MoonR), Is.EqualTo(MoonR).Within(1e-9), "on the ground: R");
            Assert.That(NearPlanetMode.LocalFar(-50.0, MoonR), Is.EqualTo(MoonR).Within(1e-9), "below the ground: no NaN");
            Assert.That(NearPlanetMode.LocalFar(1e9, MoonR), Is.EqualTo(MaxFar), "capped");
            Assert.That(NearPlanetMode.LocalFar(1e3, 0.0), Is.EqualTo(MaxFar), "no radius: the budget");
            double prev = 0;
            for (int i = 0; i <= 1000; i++)
            {
                double f = NearPlanetMode.LocalFar(i * 5e3, EarthR);
                Assert.That(f, Is.GreaterThanOrEqualTo(prev));
                prev = f;
            }
        }

        [Test]
        public void Update_HasHysteresis_AndDoesNotChatter()
        {
            var s = default(NearPlanetState);
            Assert.That(s.Body, Is.EqualTo(-1), "default(NearPlanetState) means no body (index 0 is a real body, the Sun)");
            Assert.That(s.Near, Is.False);
            const int moon = 4;
            Assert.That(NearPlanetMode.Update(ref s, moon, 1.8 * MoonR, MoonR), Is.False, "a new body starts from the enter threshold");
            Assert.That(s.Body, Is.EqualTo(moon));
            Assert.That(NearPlanetMode.Update(ref s, moon, 0.9 * MoonR, MoonR), Is.True, "below enter: near");
            Assert.That(NearPlanetMode.Update(ref s, moon, 1.5 * MoonR, MoonR), Is.True, "inside the band: stays near");
            Assert.That(NearPlanetMode.Update(ref s, moon, 2.1 * MoonR, MoonR), Is.False, "above exit: far");
            Assert.That(NearPlanetMode.Update(ref s, moon, 1.5 * MoonR, MoonR), Is.False, "inside the band: stays far");

            // oscillation inside the band after entering: no switch at all
            NearPlanetMode.Update(ref s, moon, 0.5 * MoonR, MoonR);
            int switches = 0;
            bool last = s.Near;
            for (int i = 0; i < 1000; i++)
            {
                double alt = 1.5 * MoonR + 0.45 * MoonR * Math.Sin(i * 0.37);
                bool near = NearPlanetMode.Update(ref s, moon, alt, MoonR);
                if (near != last) switches++;
                last = near;
            }
            Assert.That(switches, Is.EqualTo(0));

            // the first update at the Sun (index 0) from the default state counts as a new body
            var fresh = default(NearPlanetState);
            NearPlanetMode.Update(ref fresh, 0, 1e12, SunR);
            Assert.That(fresh.Body, Is.EqualTo(0));
            Assert.That(fresh.Near, Is.False);
        }

        [Test]
        public void Update_ResetsOnABodyChange_AndHandlesNoBody()
        {
            var s = new NearPlanetState {Body = 6, Near = true};
            Assert.That(NearPlanetMode.Update(ref s, 5, 1e9, 3.3895e6), Is.False, "the old near flag does not survive a body change");
            Assert.That(s.Body, Is.EqualTo(5));
            s = new NearPlanetState {Body = 5, Near = false};
            Assert.That(NearPlanetMode.Update(ref s, 6, 5.6e3, 1.1e4), Is.True, "entering a small body low: near at once");
            Assert.That(s.Body, Is.EqualTo(6));

            Assert.That(NearPlanetMode.Update(ref s, -1, 0.0, MoonR), Is.False);
            Assert.That(s.Body, Is.EqualTo(-1));
            Assert.That(s.Near, Is.False);
            var v = default(NearPlanetState);
            Assert.That(NearPlanetMode.Update(ref v, 3, 10.0, 0.0), Is.False, "no radius (virtual): never near");
            Assert.That(NearPlanetMode.Update(ref v, 3, double.NaN, MoonR), Is.False, "NaN altitude");
        }

        [Test]
        public void Fill_ProjectsEveryRealBody_InTableOrder()
        {
            BodyFrameTable table = Table;
            const double t = 1.234e6;
            table.GlobalState(Id("earth"), t, out Vec3 earth, out _);
            var cam = new ProjectionCamera {Eye = earth + new Vec3(7e6, 0, 0), HorizontalFovRad = Math.PI / 3, WidthPx = 1920};
            var view = View();
            var buffer = new BodyViewEntry[table.Count];
            int n = CelestialView.Fill(table, t, in cam, view, CelestialProjection.DefaultScaledDistance, CelestialProjection.DefaultMinPixels, -1, buffer);

            int real = 0;
            for (int i = 0; i < table.Count; i++) if (!table[i].Virtual) real++;
            Assert.That(n, Is.EqualTo(real));
            Assert.That(n, Is.EqualTo(table.Count - 1), "this world has one barycenter");

            int prev = -1;
            table.GlobalState(Id("sun"), t, out Vec3 sun, out _);
            for (int k = 0; k < n; k++)
            {
                BodyViewEntry e = buffer[k];
                Assert.That(e.TableIndex, Is.GreaterThan(prev), "ascending table order");
                prev = e.TableIndex;
                Assert.That(table[e.TableIndex].Virtual, Is.False);
                Assert.That(e.NearPlanet, Is.False);
                table.GlobalState(e.TableIndex, t, out Vec3 pos, out _);
                ProjectedBody expected = CelestialProjection.Project(in cam, pos, table[e.TableIndex].Radius,
                    CelestialProjection.DefaultScaledDistance, CelestialProjection.DefaultMinPixels);
                Assert.That(e.Projection.AngularRadius, Is.EqualTo(expected.AngularRadius).Within(1e-15));
                Assert.That(Vec3.Length(e.Projection.Direction - expected.Direction), Is.LessThan(1e-12));
                Assert.That(e.Projection.Mode, Is.EqualTo(expected.Mode));
                double flux = e.TableIndex == Id("sun")
                    ? BodyFlux.SolarFlux(expected.Distance, table[e.TableIndex].Radius)
                    : BodyFlux.ReflectedFlux(pos, table[e.TableIndex].Radius, sun, cam.Eye, 0.3);
                Assert.That(e.Flux, Is.EqualTo(flux).Within(Math.Abs(flux) * 1e-12), "body " + e.TableIndex);
            }
        }

        [Test]
        public void Fill_MarksOnlyTheNearBody_AndWithoutStarsHasNoFlux()
        {
            BodyFrameTable table = Table;
            var cam = new ProjectionCamera {Eye = new Vec3(1.5e11, 0, 0), HorizontalFovRad = 1.0, WidthPx = 1000};
            var buffer = new BodyViewEntry[table.Count];
            int earth = Id("earth");
            int n = CelestialView.Fill(table, 0.0, in cam, View(), 1e5, 2.0, earth, buffer);
            int marked = 0;
            for (int k = 0; k < n; k++)
                if (buffer[k].NearPlanet)
                {
                    marked++;
                    Assert.That(buffer[k].TableIndex, Is.EqualTo(earth));
                }
            Assert.That(marked, Is.EqualTo(1));

            n = CelestialView.Fill(table, 0.0, in cam, View(), 1e5, 2.0, Id("pluto-charon"), buffer);
            for (int k = 0; k < n; k++) Assert.That(buffer[k].NearPlanet, Is.False, "a virtual near body marks nothing");

            n = CelestialView.Fill(table, 0.0, in cam, View(withStar: false), 1e5, 2.0, -1, buffer);
            for (int k = 0; k < n; k++) Assert.That(buffer[k].Flux, Is.EqualTo(0.0), "no star, no light");
        }

        [Test]
        public void Fill_PlutoAndCharon_AreTwoBodiesAroundTheirBarycenter()
        {
            BodyFrameTable table = Table;
            const double t = 5e7;
            int pluto = Id("pluto"), charon = Id("charon"), bary = Id("pluto-charon");
            table.GlobalState(bary, t, out Vec3 b, out _);
            table.GlobalState(pluto, t, out Vec3 p, out _);
            table.GlobalState(charon, t, out Vec3 c, out _);
            var cam = new ProjectionCamera {Eye = b + new Vec3(0, 0, 1e8), HorizontalFovRad = 0.5, WidthPx = 1024};
            var buffer = new BodyViewEntry[table.Count];
            int n = CelestialView.Fill(table, t, in cam, View(), 1e5, 2.0, -1, buffer);
            bool seenPluto = false, seenCharon = false;
            for (int k = 0; k < n; k++)
            {
                Assert.That(buffer[k].TableIndex, Is.Not.EqualTo(bary), "the barycenter is not drawn");
                seenPluto |= buffer[k].TableIndex == pluto;
                seenCharon |= buffer[k].TableIndex == charon;
            }
            Assert.That(seenPluto && seenCharon, Is.True);

            Vec3 a = p - b, d = c - b;
            double cos = (a.X * d.X + a.Y * d.Y + a.Z * d.Z) / (Vec3.Length(a) * Vec3.Length(d));
            // 1e-6, not tighter: world.json rounds the members' semi-major axes to 1 m, so their mean motions differ by ~9e-8 and the pair
            // slowly leaves the exact opposition (5e-5 rad at t = 5e7 s); found by the executor in 3.5c, logged in stage-3.5.md
            Assert.That(cos, Is.EqualTo(-1.0).Within(1e-6), "on opposite sides of the barycenter");
            // m_A a_A = m_B a_B, nu is proportional to the mass
            double lhs = Vec3.Length(a) * table[pluto].Nu, rhs = Vec3.Length(d) * table[charon].Nu;
            Assert.That(lhs, Is.EqualTo(rhs).Within(rhs * 1e-6));
        }

        [Test]
        public void Fill_RejectsShortSpans_AndBadNearBody()
        {
            BodyFrameTable table = Table;
            var cam = new ProjectionCamera {Eye = new Vec3(1.5e11, 0, 0), HorizontalFovRad = 1.0, WidthPx = 1000};
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () =>
                CelestialView.Fill(table, 0.0, in cam, new BodyViewParams[table.Count - 1], 1e5, 2.0, -1, new BodyViewEntry[table.Count]));
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () =>
                CelestialView.Fill(table, 0.0, in cam, View(), 1e5, 2.0, -1, new BodyViewEntry[3]));
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () =>
                CelestialView.Fill(table, 0.0, in cam, View(), 1e5, 2.0, table.Count, new BodyViewEntry[table.Count]));
        }
    }
}
