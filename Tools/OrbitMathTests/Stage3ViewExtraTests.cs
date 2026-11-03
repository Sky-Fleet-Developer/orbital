using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Orbital.Core.Flight;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>Stage 3.5b: executor's own tests for the light flux, the exposure, the light direction and the star field API.</summary>
    public class Stage3ViewExtraTests
    {
        private const double Au = 1.495978707e11;

        private static double Full(double r, double dSun, double dEye, double albedo) =>
            albedo * (r / dSun) * (r / dSun) * (Au / dEye) * (Au / dEye);

        private static void AssertFinite(double v, string name)
        {
            Assert.That(double.IsNaN(v) || double.IsInfinity(v), Is.False, name + " = " + v);
        }

        [Test]
        public void LambertPhase_KnownIntermediateValue_AndSymmetricShape()
        {
            // alpha = pi/3: (sin + (pi - a) * cos) / pi = (sqrt(3)/2 + (2pi/3) * 1/2) / pi
            double expected = (Math.Sqrt(3.0) / 2.0 + Math.PI / 3.0) / Math.PI;
            Assert.That(BodyFlux.LambertPhase(Math.PI / 3.0), Is.EqualTo(expected).Within(1e-15));

            // the function is monotone, not symmetric: the value at pi/4 is above the midpoint, at 3pi/4 below it
            double mid = BodyFlux.LambertPhase(Math.PI / 2.0);
            Assert.That(mid, Is.EqualTo(1.0 / Math.PI).Within(1e-15));
            Assert.That(BodyFlux.LambertPhase(Math.PI / 4.0), Is.GreaterThan(mid));
            Assert.That(BodyFlux.LambertPhase(3.0 * Math.PI / 4.0), Is.LessThan(mid));
        }

        [Test]
        public void ReflectedFlux_PositionOverload_ObliqueGeometry_MatchesTheScalarBuildingBlock()
        {
            // the body at the origin, the Sun on +X, the eye at 60 degrees from the Sun as seen from the body
            const double d = 1e9;
            var body = Vec3.Zero;
            var sun = new Vec3(Au, 0, 0);
            var eye = new Vec3(0.5 * d, Math.Sqrt(3.0) / 2.0 * d, 0); // cos(60) = 0.5
            const double r = 3.3895e6;
            const double albedo = 0.17;

            double byPositions = BodyFlux.ReflectedFlux(body, r, sun, eye, albedo);
            double byBlocks = Full(r, Au, d, albedo) * BodyFlux.LambertPhase(Math.PI / 3.0);
            Assert.That(byPositions, Is.EqualTo(byBlocks).Within(byBlocks * 1e-12));
        }

        [Test]
        public void ReflectedFlux_SunAndEyeBothAtTheBodyCenter_IsFinite()
        {
            var body = new Vec3(Au, 0, 0);
            double f = BodyFlux.ReflectedFlux(body, 1.7374e6, body, body, 0.3);
            AssertFinite(f, "sun == eye == body");
            Assert.That(f, Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void ReflectedFlux_ScalarBuildingBlock_ClampsDistancesToTheRadius()
        {
            const double r = 6.371e6;
            const double albedo = 0.3;
            double atSurface = BodyFlux.ReflectedFlux(r, Au, r, albedo);
            double inside = BodyFlux.ReflectedFlux(r, Au, 1.0, albedo);
            Assert.That(inside, Is.EqualTo(atSurface).Within(atSurface * 1e-12), "the eye inside sees the surface value");
            Assert.That(atSurface, Is.EqualTo(albedo).Within(albedo * 1e-12), "at 1 AU from the Sun and on the surface");
            AssertFinite(atSurface, "surface");

            double albedoClamped = BodyFlux.ReflectedFlux(r, Au, r, 3.0);
            Assert.That(albedoClamped, Is.EqualTo(atSurface / albedo).Within(atSurface / albedo * 1e-12), "albedo is clamped to 1");
        }

        [Test]
        public void SolarFlux_NoRadius_IsInverseSquareEverywhereExceptThePoint()
        {
            Assert.That(BodyFlux.SolarFlux(0.25 * Au, 0.0), Is.EqualTo(16.0).Within(1e-12));
            Assert.That(BodyFlux.SolarFlux(2.0 * Au, 0.0), Is.EqualTo(0.25).Within(1e-15));
            AssertFinite(BodyFlux.SolarFlux(0.0, 0.0), "distance 0, radius 0");
            AssertFinite(BodyFlux.SolarFlux(-5.0, 0.0), "negative distance, radius 0");
        }

        [Test]
        public void Exposure_IsContinuousAtTheCeiling_AndExtremeFluxesStayFiniteAndFloatSafe()
        {
            var p = new ExposureParams {FluxFloor = 1e-16, FluxCeiling = 1e4, MinPointBrightness = 0.05, MaxPointBrightness = 2.0};
            double atCeiling = Exposure.PointBrightness(p.FluxCeiling, p);
            Assert.That(atCeiling, Is.EqualTo(1.0).Within(1e-12));
            double justAbove = Exposure.PointBrightness(p.FluxCeiling * (1.0 + 1e-9), p);
            Assert.That(justAbove, Is.GreaterThanOrEqualTo(atCeiling));
            Assert.That(justAbove - atCeiling, Is.LessThan(1e-6), "no jump at the ceiling");

            double dim = Exposure.PointBrightness(1e-300, p);
            double bright = Exposure.PointBrightness(1e300, p);
            Assert.That(dim, Is.EqualTo(p.MinPointBrightness).Within(1e-15));
            Assert.That(bright, Is.EqualTo(p.MaxPointBrightness).Within(1e-15));
            Assert.That(float.IsFinite((float)dim) && float.IsFinite((float)bright), Is.True);
            Assert.That(Exposure.PointBrightness(double.PositiveInfinity, p), Is.EqualTo(p.MaxPointBrightness));
            Assert.That(Exposure.PointBrightness(double.NegativeInfinity, p), Is.EqualTo(0.0));
        }

        [Test]
        public void SunLight_Direction_MatchesManualNormalization_ForLargeCoordinates()
        {
            var sun = new Vec3(1e13, -2e13, 3e12);
            var eye = new Vec3(1e9, 2e9, -5e8);
            Vec3 d = SunLight.Direction(sun, eye);
            Vec3 expected = (sun - eye) / Vec3.Length(sun - eye);
            Assert.That(Vec3.Length(d - expected), Is.LessThan(1e-15));
            Assert.That(Math.Abs(Vec3.Length(d) - 1.0), Is.LessThan(1e-12));
        }

        private sealed class FixedStars : IStarField
        {
            private readonly StarPoint[] _stars =
            {
                new StarPoint {Direction = Vec3.UnitY, Flux = 1e-6, Color = new StarColor {R = 255, G = 255, B = 255}},
                new StarPoint {Direction = -Vec3.UnitY, Flux = 1e-4, Color = new StarColor {R = 200, G = 100, B = 50}},
                new StarPoint {Direction = Vec3.UnitX, Flux = 1e-12, Color = new StarColor {R = 1, G = 2, B = 3}}
            };

            public int Query(Vec3 direction, double coneHalfAngleRad, double minFlux, Span<StarPoint> buffer)
            {
                int n = 0;
                double cosCone = Math.Cos(coneHalfAngleRad);
                foreach (StarPoint star in _stars)
                {
                    if (n >= buffer.Length) break;
                    if (star.Flux < minFlux) continue;
                    if (Vec3.Dot(star.Direction, direction) < cosCone) continue;
                    buffer[n++] = star;
                }
                return n;
            }
        }

        [Test]
        public void IStarField_Contract_FiltersByConeAndFlux_AndFillsOnlyTheBufferStart()
        {
            IStarField field = new FixedStars();

            Span<StarPoint> wide = stackalloc StarPoint[4];
            int nWide = field.Query(Vec3.UnitY, Math.PI / 2.0 + 1e-6, 0.0, wide);
            Assert.That(nWide, Is.EqualTo(2), "the wide cone from +Y contains +Y and +X, not -Y");
            Assert.That(wide[0].Direction, Is.EqualTo(Vec3.UnitY));
            Assert.That(wide[1].Direction, Is.EqualTo(Vec3.UnitX));

            Span<StarPoint> narrow = stackalloc StarPoint[4];
            int nNarrow = field.Query(Vec3.UnitY, 0.1, 0.0, narrow);
            Assert.That(nNarrow, Is.EqualTo(1), "the narrow cone keeps only +Y");
            Assert.That(narrow[0].Flux, Is.EqualTo(1e-6));

            Span<StarPoint> dim = stackalloc StarPoint[4];
            int nDim = field.Query(Vec3.UnitY, Math.PI / 2.0, 1e-8, dim);
            Assert.That(nDim, Is.EqualTo(1), "the minimum flux drops the faint star");
            Assert.That(dim[0].Flux, Is.EqualTo(1e-6));

            Span<StarPoint> tiny = stackalloc StarPoint[1];
            int nTiny = field.Query(Vec3.UnitY, Math.PI / 2.0, 0.0, tiny);
            Assert.That(nTiny, Is.EqualTo(1), "the buffer length truncates the result");
            Assert.That(tiny[0].Color.R, Is.EqualTo(255));
        }

        [Test]
        public void StarPoint_IsBlittableSized_AndFloatSafe()
        {
            var star = new StarPoint
            {
                Direction = new Vec3(1.0, 0.0, 0.0),
                Flux = 1e-300,
                Color = new StarColor {R = 10, G = 20, B = 30}
            };
            float fx = (float)star.Direction.X;
            float ff = (float)star.Flux;
            Assert.That(float.IsFinite(fx) && float.IsFinite(ff), Is.True);
        }
    }

    /// <summary>Stage 3.5c: executor's own tests for the near planet mode, the Local far plane and the whole-table projection.</summary>
    public class Stage3NearViewExtraTests
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
        public void NearPlanetState_DefaultMeansNoBody_AndInitializersWork()
        {
            var d = default(NearPlanetState);
            Assert.That(d.Body, Is.EqualTo(-1));
            Assert.That(d.Near, Is.False);
            var s = new NearPlanetState {Body = 6, Near = true};
            Assert.That(s.Body, Is.EqualTo(6));
            Assert.That(s.Near, Is.True);
            s.Body = -1;
            Assert.That(s.Body, Is.EqualTo(-1));
            var z = new NearPlanetState {Body = 0, Near = false};
            Assert.That(z.Body, Is.EqualTo(0), "index 0 is a real body, the Sun");
        }

        [Test]
        public void Thresholds_SmallBodyIsTwoRadii_BigBodyIsLimitedByTheFarBudget()
        {
            const double phobosR = 1.13e4;
            Assert.That(NearPlanetMode.ExitAltitude(phobosR), Is.EqualTo(2 * phobosR).Within(1e-9));
            Assert.That(NearPlanetMode.EnterAltitude(phobosR), Is.EqualTo(phobosR).Within(1e-9));

            // just below the budget the band is tiny but positive (1e-9 is below double resolution here: R^2 + (maxFar-R)^2 rounds back to R^2)
            double nearBudget = NearPlanetMode.DefaultMaxFar * (1.0 - 1e-4);
            double exit = NearPlanetMode.ExitAltitude(nearBudget);
            Assert.That(exit, Is.GreaterThan(0.0));
            Assert.That(exit, Is.LessThan(2 * nearBudget));
            Assert.That(NearPlanetMode.ExitAltitude(NearPlanetMode.DefaultMaxFar), Is.EqualTo(0.0));
            Assert.That(NearPlanetMode.ExitAltitude(NearPlanetMode.DefaultMaxFar * 2.0), Is.EqualTo(0.0));
            Assert.That(NearPlanetMode.ExitAltitude(-1.0), Is.EqualTo(0.0));
            Assert.That(NearPlanetMode.ExitAltitude(double.NaN), Is.EqualTo(0.0));
            Assert.That(NearPlanetMode.EnterAltitude(-1.0), Is.EqualTo(0.0));
        }

        [Test]
        public void LocalFar_MatchesTheFormula_IsMonotone_AndFloatSafe()
        {
            const double r = 3.3895e6;
            double prev = 0.0;
            for (int i = 0; i <= 1000; i++)
            {
                double h = i * 2e4;
                double f = NearPlanetMode.LocalFar(h, r);
                double expected = Math.Sqrt(h * (2.0 * r + h)) + r;
                if (expected <= NearPlanetMode.DefaultMaxFar)
                    Assert.That(f, Is.EqualTo(expected).Within(expected * 1e-12), "h = " + h);
                else
                    Assert.That(f, Is.EqualTo(NearPlanetMode.DefaultMaxFar), "capped at h = " + h);
                Assert.That(f, Is.GreaterThanOrEqualTo(prev), "monotone at h = " + h);
                Assert.That(float.IsFinite((float)f), Is.True);
                prev = f;
            }
            Assert.That(NearPlanetMode.LocalFar(-123.0, r), Is.EqualTo(r).Within(1e-9), "below the ground: R");
            Assert.That(NearPlanetMode.LocalFar(double.NaN, r), Is.EqualTo(r).Within(1e-9), "NaN altitude: the ground value");
            Assert.That(NearPlanetMode.LocalFar(0.0, 0.0), Is.EqualTo(NearPlanetMode.DefaultMaxFar));
        }

        [Test]
        public void Update_CrossingTheBand_SwitchesOnlyAtTheThresholds()
        {
            var s = default(NearPlanetState);
            const double r = 1.7374e6;
            const int body = 4;
            Assert.That(NearPlanetMode.Update(ref s, body, 0.0, r), Is.True, "on the ground: near at once");
            bool state = s.Near;
            for (int i = 1; i <= 1000; i++)
            {
                double alt = 2.4 * r * i / 1000.0;
                bool now = NearPlanetMode.Update(ref s, body, alt, r);
                if (now != state)
                {
                    Assert.That(alt, Is.InRange(1.95 * r, 2.05 * r), "leaves only around the exit altitude, got h = " + alt);
                    state = now;
                }
            }
            Assert.That(state, Is.False);
            for (int i = 1000; i >= 0; i--)
            {
                double alt = 2.4 * r * i / 1000.0;
                bool now = NearPlanetMode.Update(ref s, body, alt, r);
                if (now != state)
                {
                    Assert.That(alt, Is.InRange(0.95 * r, 1.05 * r), "enters only around the enter altitude, got h = " + alt);
                    state = now;
                }
            }
            Assert.That(state, Is.True);
        }

        [Test]
        public void Update_AtTheExactThresholds_DoesNotChatter()
        {
            var s = default(NearPlanetState);
            const double r = 1.7374e6;
            const int body = 4;
            NearPlanetMode.Update(ref s, body, 1.0 * r, r); // h == enter on a new body: stays out
            Assert.That(s.Near, Is.False);
            NearPlanetMode.Update(ref s, body, 1.0 * r, r);
            Assert.That(s.Near, Is.False);
            NearPlanetMode.Update(ref s, body, 0.999999 * r, r);
            Assert.That(s.Near, Is.True);
            NearPlanetMode.Update(ref s, body, 2.0 * r, r); // h == exit: leaves
            Assert.That(s.Near, Is.False);
            NearPlanetMode.Update(ref s, body, 2.0 * r, r);
            Assert.That(s.Near, Is.False, "at the exit altitude it does not re-enter");
        }

        [Test]
        public void Update_EarthBand_WorksAroundTheFarBudgetAltitude()
        {
            var s = default(NearPlanetState);
            const double r = 6.371e6;
            const int body = 3;
            double exit = NearPlanetMode.ExitAltitude(r);
            Assert.That(NearPlanetMode.Update(ref s, body, exit * 1.01, r), Is.False, "a new body starts from the enter threshold");
            Assert.That(NearPlanetMode.Update(ref s, body, exit * 0.9, r), Is.False, "inside the band: still out");
            Assert.That(NearPlanetMode.Update(ref s, body, exit * 0.4, r), Is.True, "below the enter altitude");
            Assert.That(NearPlanetMode.Update(ref s, body, exit * 0.9, r), Is.True, "inside the band: stays near");
            Assert.That(NearPlanetMode.Update(ref s, body, exit * 1.01, r), Is.False, "above the exit altitude");
            Assert.That(NearPlanetMode.Update(ref s, body, exit * 0.9, r), Is.False, "inside the band: stays out");
        }

        [Test]
        public void Update_NoBody_VirtualParent_AndNaN_AreNeverNear()
        {
            var s = default(NearPlanetState);
            Assert.That(NearPlanetMode.Update(ref s, -1, 0.0, 1.7374e6), Is.False);
            Assert.That(s.Body, Is.EqualTo(-1));
            Assert.That(NearPlanetMode.Update(ref s, 3, 10.0, 0.0), Is.False, "no radius (virtual parent)");
            Assert.That(s.Body, Is.EqualTo(3));
            Assert.That(NearPlanetMode.Update(ref s, 3, 1.0, 0.0), Is.False);
            Assert.That(NearPlanetMode.Update(ref s, 3, double.NaN, 1.7374e6), Is.False, "NaN altitude");
        }

        [Test]
        public void Fill_MatchesPerBodyCalls_AndMarksOnlyTheNearBody()
        {
            BodyFrameTable table = Table;
            const double t = 1e6;
            int earth = Id("earth");
            table.GlobalState(earth, t, out Vec3 earthPos, out _);
            var cam = new ProjectionCamera {Eye = earthPos + new Vec3(0, 8e6, 0), HorizontalFovRad = Math.PI / 3, WidthPx = 1920};
            var view = new BodyViewParams[table.Count];
            for (int i = 0; i < view.Length; i++) view[i].Albedo = 0.3;
            view[Id("sun")].Star = true;
            var buffer = new BodyViewEntry[table.Count];
            int n = CelestialView.Fill(table, t, in cam, view, CelestialProjection.DefaultScaledDistance,
                CelestialProjection.DefaultMinPixels, earth, buffer);

            Assert.That(n, Is.EqualTo(table.Count - 1), "all real bodies, one barycenter skipped");
            table.GlobalState(Id("sun"), t, out Vec3 sun, out _);
            int marked = 0;
            for (int k = 0; k < n; k++)
            {
                BodyViewEntry e = buffer[k];
                Assert.That(table[e.TableIndex].Virtual, Is.False);
                table.GlobalState(e.TableIndex, t, out Vec3 pos, out _);
                ProjectedBody expected = CelestialProjection.Project(in cam, pos, table[e.TableIndex].Radius,
                    CelestialProjection.DefaultScaledDistance, CelestialProjection.DefaultMinPixels);
                Assert.That(Vec3.Length(e.Projection.Direction - expected.Direction), Is.LessThan(1e-12));
                Assert.That(e.Projection.ScaledRadius, Is.EqualTo(expected.ScaledRadius).Within(expected.ScaledRadius * 1e-12));
                Assert.That(e.Projection.Mode, Is.EqualTo(expected.Mode));
                double flux = e.TableIndex == Id("sun")
                    ? BodyFlux.SolarFlux(expected.Distance, table[e.TableIndex].Radius)
                    : BodyFlux.ReflectedFlux(pos, table[e.TableIndex].Radius, sun, cam.Eye, 0.3);
                Assert.That(e.Flux, Is.EqualTo(flux).Within(Math.Abs(flux) * 1e-12), "body " + e.TableIndex);
                if (e.NearPlanet)
                {
                    marked++;
                    Assert.That(e.TableIndex, Is.EqualTo(earth));
                }
            }
            Assert.That(marked, Is.EqualTo(1));
        }

        [Test]
        public void Fill_OnlyTheFirstStarIsSelfLuminous_TheSecondFlagIsIgnored()
        {
            BodyFrameTable table = Table;
            const double t = 2e6;
            var cam = new ProjectionCamera {Eye = new Vec3(1.5e11, 0, 0), HorizontalFovRad = 1.0, WidthPx = 1000};
            var view = new BodyViewParams[table.Count];
            for (int i = 0; i < view.Length; i++) view[i].Albedo = 0.4;
            int sunI = Id("sun"), earthI = Id("earth"), moonI = Id("moon");
            view[sunI].Star = true;
            view[earthI].Star = true; // a second star flag: the first one stays the only light source and the only self-luminous body
            var buffer = new BodyViewEntry[table.Count];
            int n = CelestialView.Fill(table, t, in cam, view, 1e5, 2.0, -1, buffer);

            table.GlobalState(sunI, t, out Vec3 sun, out _);
            table.GlobalState(earthI, t, out Vec3 earthPos, out _);
            table.GlobalState(moonI, t, out Vec3 moonPos, out _);
            for (int k = 0; k < n; k++)
            {
                BodyViewEntry e = buffer[k];
                if (e.TableIndex == earthI)
                {
                    double expected = BodyFlux.ReflectedFlux(earthPos, table[earthI].Radius, sun, cam.Eye, 0.4);
                    Assert.That(e.Flux, Is.EqualTo(expected).Within(expected * 1e-12), "the second star flag is ignored");
                }
                if (e.TableIndex == moonI)
                {
                    double expected = BodyFlux.ReflectedFlux(moonPos, table[moonI].Radius, sun, cam.Eye, 0.4);
                    Assert.That(e.Flux, Is.EqualTo(expected).Within(expected * 1e-12), "the Moon is lit by the first star");
                }
            }
        }

        [Test]
        public void Fill_ExactSizedBuffer_Works_AndBadArgumentsThrow()
        {
            BodyFrameTable table = Table;
            int physical = 0;
            for (int i = 0; i < table.Count; i++) if (!table[i].Virtual) physical++;
            var cam = new ProjectionCamera {Eye = new Vec3(1.5e11, 0, 0), HorizontalFovRad = 1.0, WidthPx = 1000};
            var view = new BodyViewParams[table.Count];
            view[Id("sun")].Star = true;
            var exact = new BodyViewEntry[physical];
            Assert.That(CelestialView.Fill(table, 1e5, in cam, view, 1e5, 2.0, -1, exact), Is.EqualTo(physical));

            var cam0 = default(ProjectionCamera);
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () =>
                CelestialView.Fill(null, 0.0, in cam0, new BodyViewParams[1], 1e5, 2.0, -1, new BodyViewEntry[1]));
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () =>
                CelestialView.Fill(table, 0.0, in cam0, view, 1e5, 2.0, -2, new BodyViewEntry[table.Count]));
        }
    }
}
