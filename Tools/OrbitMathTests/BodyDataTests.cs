using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ara3D;
using NUnit.Framework;
using Orbital.Core;
using Orbital.Core.Bodies;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 1.5b: body data (radius, rotation) in the model facets of Core, the real radius in the minimum sphere of influence,
    /// and the numbers of the migrated BodiesTable asset against world.json (generated from the asset before the migration, byte-identical after it).
    /// </summary>
    public class BodyDataTests
    {
        private const double MarsNu = 4.282837e13;
        private const double G = MassUtility.G;

        private sealed class FakeBody : IStaticBody
        {
            public double Radius { get; set; }
            public IMassSystem MassSystem => null;
            public IStaticBody Parent { get; set; }
            public IEnumerable<IStaticBody> Children => Array.Empty<IStaticBody>();
            public DVector3 LocalPosition => DVector3.Zero;
            public StaticOrbit Orbit { get; set; }
            public double GravParameter { get; set; }
            public bool IsSatellite => true;
        }

        private static string FindUp(params string[] parts)
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static string AssetPath => FindUp("Assets", "_game", "ViewSettings", "BodiesTable.asset");
        private static string WorldPath => FindUp("orbital-server", "OrbitServer", "world.json");

        private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>Per body name (lower case): class -> field -> text, from the SerializeReference part of the asset.</summary>
        private static Dictionary<string, Dictionary<string, Dictionary<string, string>>> ReadAsset(string path)
        {
            string[] lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
            Dictionary<string, string> owner = new Dictionary<string, string>();
            var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            string name = null;
            int i = 0;
            for (; i < lines.Length && !lines[i].StartsWith("  references:"); i++)
            {
                if (lines[i].StartsWith("  - name: "))
                {
                    name = lines[i].Substring(10).Trim().ToLowerInvariant();
                    result[name] = new Dictionary<string, Dictionary<string, string>>();
                }
                else if (lines[i].StartsWith("    - rid: ") && name != null) owner[lines[i].Substring(11).Trim()] = name;
            }
            string rid = null, cls = null;
            bool data = false;
            for (; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.StartsWith("    - rid: ")) { rid = l.Substring(11).Trim(); cls = null; data = false; }
                else if (l.StartsWith("      type: {class: ")) cls = l.Substring(20).Split(',')[0];
                else if (l.StartsWith("      data:")) { data = true; result[owner[rid]][cls] = new Dictionary<string, string>(); }
                else if (data && l.StartsWith("        ") && l.Contains(": "))
                {
                    int colon = l.IndexOf(": ", StringComparison.Ordinal);
                    result[owner[rid]][cls][l.Substring(8, colon - 8)] = l.Substring(colon + 2).Trim();
                }
            }
            return result;
        }

        [Test]
        public void ModelFacets_HaveUniqueIdsAndAreFoundByType()
        {
            Assert.AreEqual(3, FacetId<PhysicalFacet>.Value);
            Assert.AreEqual(4, FacetId<RotationFacet>.Value);
            BodyRecord record = new BodyRecord { name = "Phobos[2]" };
            record.facets.Add(new PhysicalFacet { radiusMeters = 11267 });
            record.facets.Add(new RotationFacet { tidallyLocked = true });
            record.Rebuild();
            Assert.IsTrue(record.HasFacet<PhysicalFacet>());
            Assert.IsTrue(record.HasFacet<RotationFacet>());
            Assert.AreEqual(11267, record.GetFacet<PhysicalFacet>().radiusMeters);
            Assert.IsTrue(record.GetFacet<RotationFacet>().tidallyLocked);
            Assert.AreEqual(new[] { 3, 4 }, new[] { FacetId<PhysicalFacet>.Value, FacetId<RotationFacet>.Value });
        }

        [Test]
        public void StaticBodyRadius_ComesFromThePhysicalFacet_AndFeedsTheMinimumSphere()
        {
            // Phobos: Laplace sphere is about 7 km, radius 11.267 km * 1.5 = 16.9 km wins.
            double a = 9376000, phobosNu = 1.0659e16 * G;
            StaticOrbit orbit = new StaticOrbit { Nu = MarsNu };
            orbit.Calculate(new TrajectorySettings { mass = 1, eccentricity = 0.0151f, semiMajorAxis = (float) a, inclination = 0, argumentOfPeriapsis = 0, longitudeAscendingNode = 0, timeShift = 0 }, 0);
            FakeBody mars = new FakeBody { GravParameter = MarsNu };
            FakeBody phobos = new FakeBody { Parent = mars, GravParameter = phobosNu, Orbit = orbit, Radius = 11267 };
            double laplace = TrajectoryUtility.GetSphereOfInfluenceRadius(orbit.SemiMajorAxis, phobosNu, MarsNu);
            Assert.Less(laplace, 11267 * 1.5);
            Assert.AreEqual(11267 * 1.5, MassUtility.GetSphereOfInfluenceRadius(phobos), 1e-6);
            phobos.Radius = 0; // no record: the Laplace sphere alone
            Assert.AreEqual(laplace, MassUtility.GetSphereOfInfluenceRadius(phobos), 1e-6);
            Assert.IsTrue(double.IsPositiveInfinity(MassUtility.GetSphereOfInfluenceRadius(mars)));
        }

        [Test]
        public void Asset_AllBodiesHaveModelFacets_AndAppearanceKeepsOnlyTheLook()
        {
            string path = AssetPath;
            if (path == null) Assert.Ignore("BodiesTable.asset not found");
            var asset = ReadAsset(path);
            Assert.AreEqual(31, asset.Count, "one record per body; the Pluto-Charon barycenter has none");
            int withRadius = 0;
            foreach (var pair in asset)
            {
                Assert.IsTrue(pair.Value.ContainsKey("PhysicalFacet"), pair.Key + " has no PhysicalFacet");
                Assert.IsTrue(pair.Value.ContainsKey("RotationFacet"), pair.Key + " has no RotationFacet");
                Assert.IsTrue(pair.Value.ContainsKey("AppearanceFacet"), pair.Key + " has no AppearanceFacet");
                Assert.Greater(D(pair.Value["PhysicalFacet"]["radiusMeters"]), 0, pair.Key);
                Assert.IsFalse(pair.Value["AppearanceFacet"].ContainsKey("radiusMeters"), pair.Key);
                Assert.IsFalse(pair.Value["AppearanceFacet"].ContainsKey("rotationPeriodSeconds"), pair.Key);
                foreach (string field in new[] { "rotationPeriodSeconds", "axialTiltDegrees", "axisAzimuthDegrees", "initialAngleDegrees", "tidallyLocked" })
                    Assert.IsTrue(pair.Value["RotationFacet"].ContainsKey(field), pair.Key + " " + field);
                withRadius++;
            }
            Assert.AreEqual(31, withRadius);
            Assert.AreEqual(11267.0, D(asset["phobos"]["PhysicalFacet"]["radiusMeters"]));
            Assert.AreEqual("1", asset["phobos"]["RotationFacet"]["tidallyLocked"]);
            Assert.AreEqual(6371000.0, D(asset["earth"]["PhysicalFacet"]["radiusMeters"]));
            Assert.AreEqual(86164.0905, D(asset["earth"]["RotationFacet"]["rotationPeriodSeconds"]));
            Assert.AreEqual(177.36, D(asset["venus"]["RotationFacet"]["axialTiltDegrees"]));
            Assert.AreEqual(6.68, D(asset["moon"]["RotationFacet"]["axialTiltDegrees"]));
        }

        [Test]
        public void Asset_NumbersEqualWorldJson_ForAll31Bodies()
        {
            string assetPath = AssetPath, worldPath = WorldPath;
            if (assetPath == null || worldPath == null) Assert.Ignore("asset or server world.json not found (sibling repository orbital-server)");
            var asset = ReadAsset(assetPath);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(worldPath));
            int checkedBodies = 0;
            foreach (JsonElement b in doc.RootElement.GetProperty("bodies").EnumerateArray())
            {
                if (b.TryGetProperty("virtual", out JsonElement v) && v.GetBoolean()) continue;
                string name = b.GetProperty("name").GetString().ToLowerInvariant();
                var f = asset[name];
                Assert.AreEqual(D(f["PhysicalFacet"]["radiusMeters"]), b.GetProperty("radius").GetDouble(), name + " radius");
                var rot = f["RotationFacet"];
                double period = D(rot["rotationPeriodSeconds"]);
                bool locked = rot["tidallyLocked"] == "1";
                if (period > 0 || locked)
                {
                    JsonElement r = b.GetProperty("rotation");
                    Assert.AreEqual(period, r.GetProperty("periodSeconds").GetDouble(), name + " period");
                    Assert.AreEqual(D(rot["axialTiltDegrees"]), r.GetProperty("obliquityDeg").GetDouble(), name + " tilt");
                    Assert.AreEqual(D(rot["axisAzimuthDegrees"]), r.GetProperty("axisAzimuthDeg").GetDouble(), name + " azimuth");
                    Assert.AreEqual(D(rot["initialAngleDegrees"]), r.GetProperty("initialAngleDeg").GetDouble(), name + " initial angle");
                    Assert.AreEqual(locked, r.GetProperty("tidallyLocked").GetBoolean(), name + " lock");
                }
                else Assert.IsFalse(b.TryGetProperty("rotation", out _), name + " does not spin");
                checkedBodies++;
            }
            Assert.AreEqual(31, checkedBodies);
        }

        [Test]
        public void RealRadii_MinimumSphereChangesOnlyBodiesWhoseLaplaceSphereIsSmall()
        {
            string assetPath = AssetPath, worldPath = WorldPath;
            if (assetPath == null || worldPath == null) Assert.Ignore("asset or server world.json not found");
            var asset = ReadAsset(assetPath);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(worldPath));
            var bodies = doc.RootElement.GetProperty("bodies").EnumerateArray().ToDictionary(b => b.GetProperty("id").GetString());
            List<string> raised = new List<string>();
            foreach (JsonElement b in bodies.Values)
            {
                if (!b.TryGetProperty("parent", out JsonElement parentId)) continue;
                JsonElement parent = bodies[parentId.GetString()];
                if (!b.TryGetProperty("radius", out JsonElement radiusElement) || radiusElement.GetDouble() <= 0) continue;
                double a = b.GetProperty("orbit").GetProperty("semiMajorAxis").GetDouble();
                double radius = D(asset[b.GetProperty("name").GetString().ToLowerInvariant()]["PhysicalFacet"]["radiusMeters"]);
                double laplace = TrajectoryUtility.GetSphereOfInfluenceRadius(a, b.GetProperty("nu").GetDouble(), parent.GetProperty("nu").GetDouble());
                double withRadius = TrajectoryUtility.GetSphereOfInfluenceRadius(a, b.GetProperty("nu").GetDouble(), parent.GetProperty("nu").GetDouble(), radius);
                Assert.GreaterOrEqual(withRadius, laplace);
                if (withRadius > laplace) raised.Add(b.GetProperty("name").GetString());
            }
            TestContext.WriteLine("Minimum sphere raises the SOI of: " + string.Join(", ", raised));
            Assert.Contains("Phobos", raised);
            Assert.IsFalse(raised.Contains("Moon"));
            Assert.IsFalse(raised.Contains("Earth"));
            Assert.IsFalse(raised.Contains("Titan"));
        }
    }
}
