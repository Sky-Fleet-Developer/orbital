using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ara3D;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Worlds;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 2c.W: the client world comes from the shared file Assets/_game/Config/world.json (the server format). The engine-free loader must give
    /// the same world the scene tree of Assets/Main.unity gave (checked by an explicit test only since 2c.W2) and the same positions the
    /// server computes from the file (orbit epoch = epochZero + orbit.epoch, stage 2c.W2).
    /// </summary>
    public class WorldFileTests
    {
        // Moments to compare: game time 0, a day, a year, about 32 years and a negative time (before the epoch of most bodies).
        private static readonly double[] Times = {0.0, 86400.0, 3.15576e7, 1.0e9, -3.0e8};

        private static string RepoDir([CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", ".."));
        private static string WorldPath => Path.Combine(RepoDir(), "Assets", "_game", "Config", WorldFileHash.FileName);
        private static string ScenePath => Path.Combine(RepoDir(), "Assets", "Main.unity");

        [Test]
        public void SharedWorld_Loads_WithoutWarnings_AndHasTheSceneShapes()
        {
            string text = File.ReadAllText(WorldPath);
            LoadedWorld world = WorldFileLoader.Load(text);
            Assert.IsEmpty(world.Warnings, string.Join("\n", world.Warnings));
            Assert.AreEqual(WorldFileHash.Compute(text), world.Hash);
            Assert.AreEqual(32, world.Definition.Bodies.Count, "31 bodies of BodiesTable and the barycenter Pluto-Charon");
            Assert.IsInstanceOf<SingleCenterBranch>(world.Root);
            Assert.AreEqual("sun", world.BodyOf[world.Root].Id);
            Assert.AreEqual("sun", world.BodyOf[((SingleCenterBranch) world.Root).Central].Id);
            DoubleSystemBranch pair = world.Root.GetRecursively().OfType<DoubleSystemBranch>().Single();
            Assert.AreEqual("pluto-charon", world.BodyOf[pair].Id);
            Assert.AreEqual("pluto", world.BodyOf[pair.ChildA].Id);
            Assert.AreEqual("charon", world.BodyOf[pair.ChildB].Id);
            // every body of the file is in the tree
            CollectionAssert.AreEquivalent(world.Definition.Bodies.Select(b => b.Id), world.BodyOf.Values.Select(b => b.Id).Distinct());
        }

        /// <summary>
        /// The tree from the file and the tree of the scene are the same numbers (float32 settings, same shapes), so the positions must be equal
        /// to the bit; the test allows 1e-9 m only to state "equal" for doubles without asserting the evaluation order.
        /// </summary>
        // User decision 2c.W (2026-11-03): the client compares its world with the server only; the scene tree is not kept in sync with the file.
        // This check is kept for a manual run (it fails as soon as the file is edited by hand).
        [Test, Explicit("the scene tree is not synced with the world file (user decision 2c.W); run by hand when needed")]
        public void SharedWorld_GivesTheSceneTreePositions()
        {
            LoadedWorld world = WorldFileLoader.Load(File.ReadAllText(WorldPath));
            Dictionary<string, IMassSystem> fromFile = new Dictionary<string, IMassSystem>();
            foreach (KeyValuePair<IMassSystem, WorldBodyDef> pair in world.BodyOf)
            {
                // the branch (with the orbit) is the body; a central mass sits at the center of its branch; the root branch has no scene object of its own
                if (pair.Key == world.Root) continue;
                if (!fromFile.ContainsKey(pair.Value.Id) || !(pair.Key is CelestialBody)) fromFile[pair.Value.Id] = pair.Key;
            }

            (IMassSystem sceneRoot, Dictionary<IMassSystem, string> sceneNames) = ReadSceneTree();
            Dictionary<string, IMassSystem> fromScene = new Dictionary<string, IMassSystem>();
            foreach (KeyValuePair<IMassSystem, string> pair in sceneNames)
            {
                string id = WorldFileLoader.IdOf(pair.Value);
                if (!fromScene.ContainsKey(id) || !(pair.Key is CelestialBody)) fromScene[id] = pair.Key;
            }

            CollectionAssert.AreEquivalent(fromScene.Keys, fromFile.Keys, "the same bodies");
            foreach (string id in fromFile.Keys)
            {
                Assert.AreEqual(fromScene[id].GetType(), fromFile[id].GetType(), id + ": node type");
                Assert.AreEqual(fromScene[id].Mass, fromFile[id].Mass, id + ": mass");
                TrajectorySettings a = fromScene[id].Settings, b = fromFile[id].Settings;
                Assert.AreEqual(a, b, id + ": orbit settings");
            }

            foreach (double t in Times)
            {
                Dictionary<IMassSystem, DVector3> sceneAt = GlobalPositions(sceneRoot, t), fileAt = GlobalPositions(world.Root, t);
                foreach (string id in fromFile.Keys)
                {
                    double d = (sceneAt[fromScene[id]] - fileAt[fromFile[id]]).Length();
                    Assert.That(d, Is.LessThanOrEqualTo(1e-9), $"{id} at t = {t}: {d} m");
                }
            }
        }

        /// <summary>
        /// The server builds each orbit from the double values of the file (StaticOrbit.SetOrbit, nu = orbitNu or the parent's nu) and samples it
        /// with GetSample; the client evaluates the float settings with GetPositionAtT. The elements are the same float32 numbers; nu differs at most
        /// in the last bits (G*m and the barycentric nu computed in another order, ~1e-16 relative), and both samplers solve Kepler's equation to
        /// 1e-13 rad. Measured (2026-11-03): worst difference 6e-16 of the distance from the Sun over the five moments. Tolerance 1e-12 of that
        /// distance (6 m at Pluto, 0.15 m at the Earth): a thousand times the measured value for another platform or evaluation order, and still
        /// far below a real mismatch: one element rounded to float32 on one side only shows as ~6e-8 of the distance.
        /// </summary>
        [Test]
        public void SharedWorld_GivesTheServerPositions()
        {
            LoadedWorld world = WorldFileLoader.Load(File.ReadAllText(WorldPath));
            Dictionary<string, WorldBodyDef> byId = world.Definition.Bodies.ToDictionary(b => b.Id);
            Dictionary<string, IMassSystem> fromFile = new Dictionary<string, IMassSystem>();
            foreach (KeyValuePair<IMassSystem, WorldBodyDef> pair in world.BodyOf)
            {
                if (pair.Key == world.Root) continue;
                if (!fromFile.ContainsKey(pair.Value.Id) || !(pair.Key is CelestialBody)) fromFile[pair.Value.Id] = pair.Key;
            }

            Dictionary<string, StaticOrbit> serverOrbits = new Dictionary<string, StaticOrbit>();
            const double d2r = Math.PI / 180.0;
            foreach (WorldBodyDef b in world.Definition.Bodies.Where(b => b.Parent != null))
            {
                WorldOrbitDef o = b.Orbit;
                StaticOrbit orbit = new StaticOrbit {Nu = o.OrbitNu ?? byId[b.Parent].Nu};
                orbit.SetOrbit(o.InclinationDeg * d2r, o.Eccentricity, o.SemiMajorAxis, o.LanDeg * d2r, o.AopDeg * d2r, o.MeanAnomalyDeg * d2r, world.EpochZero + o.Epoch);
                serverOrbits[b.Id] = orbit;
            }

            double worst = 0;
            foreach (double t in Times)
            {
                Dictionary<IMassSystem, DVector3> client = GlobalPositions(world.Root, t, world.EpochZero);
                foreach (WorldBodyDef b in world.Definition.Bodies)
                {
                    DVector3 server = DVector3.Zero;
                    for (WorldBodyDef x = b; x.Parent != null; x = byId[x.Parent]) server += serverOrbits[x.Id].GetSample(t).position;
                    double d = (client[fromFile[b.Id]] - server).Length();
                    double scale = Math.Max(server.Length(), 1.0);
                    worst = Math.Max(worst, d / scale);
                    Assert.That(d, Is.LessThanOrEqualTo(1e-12 * scale), $"{b.Id} at t = {t}: {d} m of {scale} m");
                }
            }

            TestContext.Progress.WriteLine($"world file vs server: worst relative position difference {worst:E2}");
        }

        [Test]
        public void Hash_IgnoresBomAndLineEnds_ButNotContent()
        {
            Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", WorldFileHash.Compute(""), "SHA-256 of nothing");
            string lf = "{\n  \"bodies\": []\n}\n";
            string crlf = lf.Replace("\n", "\r\n");
            string bom = (char) 0xFEFF + crlf;
            Assert.AreEqual(WorldFileHash.Compute(lf), WorldFileHash.Compute(crlf));
            Assert.AreEqual(WorldFileHash.Compute(lf), WorldFileHash.Compute(bom));
            Assert.AreNotEqual(WorldFileHash.Compute(lf), WorldFileHash.Compute(lf.Replace("[]", "[ ]")));
            Assert.AreEqual(64, WorldFileHash.Compute(lf).Length);
        }

        [Test]
        public void IdOf_FollowsTheGeneratorRule()
        {
            Assert.AreEqual("pluto-charon", WorldFileLoader.IdOf("Pluto-Charon"));
            Assert.AreEqual("earth", WorldFileLoader.IdOf("Earth"));
            Assert.AreEqual("a-b-2", WorldFileLoader.IdOf("  A  b_2 "));
        }

        [Test]
        public void BadFiles_GiveClearErrors_AndInexactNumbers_GiveWarnings()
        {
            const string sun = "{\"id\":\"sun\",\"name\":\"Sun\",\"nu\":1.3271645662764774e+20}";
            string Planet(string extra) =>
                "{\"id\":\"p\",\"parent\":\"sun\",\"nu\":3.986e14,\"orbit\":{\"eccentricity\":0.1,\"semiMajorAxis\":1.5e11" + extra + "}}";
            string World(params string[] bodies) => "{\"bodies\":[" + string.Join(",", bodies) + "]}";

            LoadedWorld ok = WorldFileLoader.Load(World(sun, Planet("")));
            Assert.IsInstanceOf<CelestialBody>(((SingleCenterBranch) ok.Root).Children.Single());
            Assert.IsTrue(ok.Warnings.Any(w => w.Contains("not G times a float32 mass")), "3.986e14 is not G * a float mass");

            Assert.That(() => WorldFileLoader.Load(World(sun, Planet(",\"meanAnomalyDeg\":10"))), Throws.TypeOf<WorldFileException>().With.Message.Contains("meanAnomalyDeg"));
            Assert.That(() => WorldFileLoader.Load(World(Planet(""), sun)), Throws.TypeOf<WorldFileException>().With.Message.Contains("parent sun"));
            Assert.That(() => WorldFileLoader.Load(World(sun, sun)), Throws.TypeOf<WorldFileException>().With.Message.Contains("duplicate"));
            Assert.That(() => WorldFileLoader.Load("{"), Throws.TypeOf<WorldFileException>());
            Assert.That(() => WorldFileLoader.Load(World(sun, "{\"id\":\"b\",\"parent\":\"sun\",\"nu\":1,\"virtual\":true,\"orbit\":{\"semiMajorAxis\":1e9}}")),
                Throws.TypeOf<WorldFileException>().With.Message.Contains("needs a system"));

            LoadedWorld rounded = WorldFileLoader.Load(World(sun, Planet(",\"epoch\":0.1")));
            Assert.IsTrue(rounded.Warnings.Any(w => w.Contains("epoch") && w.Contains("float32")), string.Join("\n", rounded.Warnings));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Global positions of every mass at time t, as WorldModel computes them (orbits made at load time 0, sum over the chain of parents).</summary>
        internal static Dictionary<IMassSystem, DVector3> GlobalPositions(IMassSystem root, double t, double epochZero = 0.0)
        {
            Dictionary<IMassSystem, StaticOrbit> orbits = new Dictionary<IMassSystem, StaticOrbit>();
            root.FillTrajectoriesRecursively(orbits, epochZero);
            Dictionary<IMassSystem, DVector3> result = new Dictionary<IMassSystem, DVector3> {{root, DVector3.Zero}};

            void Walk(IMassSystem parent)
            {
                foreach (IMassSystem child in parent.GetContent())
                {
                    if (child == null) continue;
                    result[child] = result[parent] + orbits[child].GetPositionAtT(t);
                    Walk(child);
                }
            }

            Walk(root);
            return result;
        }

        /// <summary>
        /// The world tree of Assets/Main.unity (World.tree.serializedValue, read as TreeContainer.Load does) and the scene object name of every mass
        /// (objects under World/RootMass named "&lt;Name&gt;[&lt;slot&gt;]"), the same reading as orbital-server/Tools/extract_scene_tree.py.
        /// </summary>
        private static (IMassSystem root, Dictionary<IMassSystem, string> names) ReadSceneTree()
        {
            string scene = File.ReadAllText(ScenePath);
            string[] lines = scene.Split('\n');
            int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("serializedValue:"));
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "serializedValue in the scene");
            List<string> parts = new List<string> {lines[start].Substring(lines[start].IndexOf("serializedValue:", StringComparison.Ordinal) + 16).Trim().Substring(1)};

            bool Closed(string s) => (s.Length - s.TrimEnd('\'').Length) % 2 == 1;
            for (int i = start + 1; !Closed(parts[parts.Count - 1]); i++) parts.Add(lines[i].Trim());
            string json = string.Join(" ", parts);
            json = json.Substring(0, json.Length - 1).Replace("''", "'");

            JsonSerializerSettings settings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto, SerializationBinder = new AnyAssemblyBinder()};
            IMassSystem root = JsonConvert.DeserializeObject<TreeHolder>(json, settings).Root;

            // GameObject names and the transform hierarchy
            Dictionary<long, string> goNames = new Dictionary<long, string>();
            Dictionary<long, (long go, long father)> transforms = new Dictionary<long, (long, long)>();
            string[] docs = Regex.Split(scene, @"^--- !u!(\d+) &(\d+)\s*$", RegexOptions.Multiline);
            for (int i = 1; i + 2 < docs.Length; i += 3)
            {
                int cls = int.Parse(docs[i]);
                long id = long.Parse(docs[i + 1]);
                string body = docs[i + 2];
                if (cls == 1) goNames[id] = Regex.Match(body, @"m_Name: (.*)").Groups[1].Value.Trim().Trim('\'');
                if (cls == 4)
                {
                    transforms[id] = (long.Parse(Regex.Match(body, @"m_GameObject: \{fileID: (\d+)").Groups[1].Value),
                        long.Parse(Regex.Match(body, @"m_Father: \{fileID: (\d+)").Groups[1].Value));
                }
            }

            long rootMass = transforms.Single(t => goNames[t.Value.go] == "RootMass").Key;
            Dictionary<IMassSystem, string> names = new Dictionary<IMassSystem, string>();

            void Map(IMassSystem mass, long transform)
            {
                int slot = -1;
                foreach (IMassSystem child in mass.GetContent())
                {
                    slot++;
                    if (child == null) continue;
                    string suffix = "[" + slot + "]";
                    long childTransform = transforms.Single(t => t.Value.father == transform && goNames[t.Value.go].EndsWith(suffix)).Key;
                    string name = goNames[transforms[childTransform].go];
                    names[child] = name.Substring(0, name.Length - suffix.Length);
                    Map(child, childTransform);
                }
            }

            Map(root, rootMass);
            return (root, names);
        }

        private class TreeHolder
        {
            [JsonProperty] public IMassSystem Root;
        }

        /// <summary>The scene names types with the Unity assembly "Core"; here they live in the test assembly.</summary>
        private class AnyAssemblyBinder : DefaultSerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                return typeof(IMassSystem).Assembly.GetType(typeName) ?? base.BindToType(assemblyName, typeName);
            }
        }
    }
}
