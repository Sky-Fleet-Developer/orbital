using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Worlds
{
    /// <summary>The world file cannot be turned into a client world (format error or something the client tree cannot express).</summary>
    public class WorldFileException : Exception
    {
        public WorldFileException(string message) : base(message)
        {
        }
    }

    /// <summary>Result of <see cref="WorldFileLoader"/>: the mass tree for <see cref="WorldModel"/> and the body of the file behind every mass.</summary>
    public sealed class LoadedWorld
    {
        internal LoadedWorld(IMassSystem root, WorldFileDef definition, string hash, Dictionary<IMassSystem, WorldBodyDef> bodyOf, List<string> warnings)
        {
            Root = root;
            Definition = definition;
            Hash = hash;
            BodyOf = bodyOf;
            Warnings = warnings;
        }

        /// <summary>Root of the mass tree, the same shapes the scene tree has (see <see cref="WorldFileLoader"/>).</summary>
        public IMassSystem Root { get; }
        public WorldFileDef Definition { get; }
        /// <summary><see cref="WorldFileHash"/> of the text, null when built from a definition.</summary>
        public string Hash { get; }
        /// <summary>Body of the file for every mass of the tree. The central mass of a branch maps to the same body as the branch; the root branch to the root body.</summary>
        public IReadOnlyDictionary<IMassSystem, WorldBodyDef> BodyOf { get; }
        /// <summary>World zero epoch of the file: the model is built at this time (orbit epoch = EpochZero + orbit.epoch, as on the server).</summary>
        public double EpochZero => Definition.EpochZero;
        /// <summary>What the client tree cannot reproduce exactly (float32 rounding, ignored fields); empty for the shipped world.</summary>
        public IReadOnlyList<string> Warnings { get; }
    }

    /// <summary>
    /// Builds the client mass tree (<see cref="IMassSystem"/>) from the shared world file (stage 2c.W). Engine-free: linked into Tools/OrbitMathTests.
    /// The tree has the shapes the scene tree has always had, so <see cref="WorldModel"/>, <see cref="MassUtility.FillTrajectoriesRecursively"/>
    /// and the views work unchanged:
    /// - the root body: a <see cref="SingleCenterBranch"/> with empty settings, its body as the Central (slot 0) and its children (slots 1..);
    /// - a body with children: a <see cref="SingleCenterBranch"/> on the body's orbit, the body itself as the Central (mass only), the children after it;
    /// - a body without children: a <see cref="CelestialBody"/>;
    /// - a virtual body with a barycentric system of two members: a <see cref="DoubleSystemBranch"/> on its orbit, ChildA/ChildB = the members in the
    ///   order of system.members.
    /// Children keep the order of the file. Masses are nu / G, orbit elements go into the float fields of <see cref="TrajectorySettings"/>
    /// (epoch = timeShift, mean anomaly 0 at the epoch), exactly like the scene tree; the file holds float32 values, anything else is reported in
    /// <see cref="LoadedWorld.Warnings"/>. The orbit nu of the members of a double system is derived from the masses as Core does; a file value that
    /// differs is reported too.
    /// </summary>
    public static class WorldFileLoader
    {
        // relative difference above which a derived number is reported as not the one of the file
        private const double DerivedTolerance = 1e-12;

        public static LoadedWorld Load(string json)
        {
            return Build(Parse(json), WorldFileHash.Compute(json));
        }

        public static WorldFileDef Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new WorldFileException("the world file is empty");
            try
            {
                JsonSerializer serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                });
                using JsonTextReader reader = new JsonTextReader(new StringReader(json));
                return serializer.Deserialize<WorldFileDef>(reader) ?? throw new WorldFileException("the world file is not a JSON object");
            }
            catch (JsonException e)
            {
                throw new WorldFileException("the world file is not valid JSON: " + e.Message);
            }
        }

        /// <summary>Id of a body by its name, the rule of orbital-server/Tools/convert_scene_world.py: lowercase, runs of other characters become one dash.</summary>
        public static string IdOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            StringBuilder result = new StringBuilder(name.Length);
            bool dash = false;
            foreach (char c in name.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (dash && result.Length > 0) result.Append('-');
                    dash = false;
                    result.Append(c);
                }
                else
                {
                    dash = true;
                }
            }

            return result.ToString();
        }

        public static LoadedWorld Build(WorldFileDef def, string hash = null)
        {
            if (def?.Bodies == null || def.Bodies.Count == 0) throw new WorldFileException("the world file has no bodies");
            List<string> warnings = new List<string>();
            Dictionary<string, WorldBodyDef> byId = new Dictionary<string, WorldBodyDef>();
            Dictionary<string, List<WorldBodyDef>> children = new Dictionary<string, List<WorldBodyDef>>();
            WorldBodyDef root = null;
            foreach (WorldBodyDef body in def.Bodies)
            {
                if (body == null || string.IsNullOrWhiteSpace(body.Id)) throw new WorldFileException("a body has no id");
                string name = Label(body);
                if (byId.ContainsKey(body.Id)) throw new WorldFileException("duplicate body id " + body.Id);
                if (!IsFinite(body.Nu) || body.Nu < 0) throw new WorldFileException(name + ": nu must be a finite number >= 0");
                if (!body.Virtual && !(body.Nu > 0)) throw new WorldFileException(name + ": a body with a surface needs nu > 0");
                if (body.Parent == null)
                {
                    if (root != null) throw new WorldFileException("several root bodies: " + root.Id + " and " + body.Id);
                    root = body;
                    if (body.Orbit != null) warnings.Add(name + ": the root body has an orbit, it is ignored");
                }
                else
                {
                    if (!byId.ContainsKey(body.Parent)) throw new WorldFileException(name + ": parent " + body.Parent + " is unknown or comes after the body");
                    if (body.Orbit == null) throw new WorldFileException(name + ": has a parent but no orbit");
                    CheckOrbit(body, warnings);
                    children[body.Parent].Add(body);
                }

                if (body.SoiRadius != null) warnings.Add(name + ": soiRadius is a server override, the client uses the Laplace sphere");
                byId.Add(body.Id, body);
                children.Add(body.Id, new List<WorldBodyDef>());
            }

            if (root == null) throw new WorldFileException("the world file has no root body (a body without parent)");
            if (!IsFinite(def.EpochZero)) throw new WorldFileException("epochZero must be a finite number");

            Dictionary<IMassSystem, WorldBodyDef> bodyOf = new Dictionary<IMassSystem, WorldBodyDef>();

            IMassSystem Node(WorldBodyDef body)
            {
                List<WorldBodyDef> kids = children[body.Id];
                if (body.System != null || body.Virtual)
                {
                    return DoubleSystem(body, kids);
                }

                if (body == root || kids.Count > 0)
                {
                    CelestialBody central = new CelestialBody {Settings = new TrajectorySettings {mass = MassOf(body, warnings)}};
                    bodyOf[central] = body;
                    SingleCenterBranch branch = new SingleCenterBranch(central, kids.Select(Node).ToList())
                    {
                        Settings = body == root ? default : OrbitSettings(body, warnings),
                    };
                    bodyOf[branch] = body;
                    return branch;
                }

                CelestialBody leaf = new CelestialBody {Settings = OrbitSettings(body, warnings)};
                bodyOf[leaf] = body;
                return leaf;
            }

            IMassSystem DoubleSystem(WorldBodyDef body, List<WorldBodyDef> kids)
            {
                string name = Label(body);
                if (!body.Virtual) throw new WorldFileException(name + ": a body with a system must be virtual (a barycenter)");
                if (body.System == null) throw new WorldFileException(name + ": a virtual body needs a system (the client knows only barycenters of double systems)");
                if (body == root) throw new WorldFileException(name + ": the root body cannot be a double system in the client tree");
                if (!string.Equals(body.System.Kind ?? "barycentric", "barycentric", StringComparison.OrdinalIgnoreCase))
                    throw new WorldFileException(name + ": unknown system kind " + body.System.Kind);
                List<string> members = body.System.Members ?? new List<string>();
                if (members.Count != 2) throw new WorldFileException(name + ": the client supports double systems only (2 members), found " + members.Count);
                if (kids.Count != 2 || !members.All(m => kids.Any(k => k.Id == m)))
                    throw new WorldFileException(name + ": the children of a barycenter must be exactly its members " + string.Join(", ", members));
                WorldBodyDef a = byId[members[0]], b = byId[members[1]];
                IMassSystem nodeA = Node(a), nodeB = Node(b);
                double massA = nodeA.Mass, massB = nodeB.Mass;
                CheckBarycentricNu(a, massA, massB, warnings);
                CheckBarycentricNu(b, massB, massA, warnings);
                TrajectorySettings settings = OrbitSettings(body, warnings, false);
                settings.mass = (float) (massA + massB);
                if (Math.Abs(body.Nu - (massA + massB) * MassUtility.G) > 1e-6 * body.Nu)
                    warnings.Add(name + ": nu " + body.Nu.ToString("R", CultureInfo.InvariantCulture) + " is not G * (m_A + m_B); the client uses the sum of the member masses");
                DoubleSystemBranch branch = new DoubleSystemBranch(nodeA, nodeB) {Settings = settings};
                bodyOf[branch] = body;
                return branch;
            }

            IMassSystem tree = Node(root);
            foreach (WorldBodyDef body in def.Bodies)
            {
                if (body.Parent != null && body.Orbit?.OrbitNu != null && !byId[body.Parent].Virtual)
                {
                    double parentNu = (double) MassOf(byId[body.Parent], null) * MassUtility.G;
                    if (!Near(body.Orbit.OrbitNu.Value, parentNu))
                        warnings.Add(Label(body) + ": orbitNu differs from the parent's nu; the client uses the parent's mass");
                }
            }

            return new LoadedWorld(tree, def, hash, bodyOf, warnings);
        }

        private static void CheckOrbit(WorldBodyDef body, List<string> warnings)
        {
            WorldOrbitDef o = body.Orbit;
            string name = Label(body);
            foreach (double v in new[] {o.InclinationDeg, o.Eccentricity, o.SemiMajorAxis, o.LanDeg, o.AopDeg, o.MeanAnomalyDeg, o.Epoch})
            {
                if (!IsFinite(v)) throw new WorldFileException(name + ": orbit elements must be finite numbers");
            }

            if (o.MeanAnomalyDeg != 0)
                throw new WorldFileException(name + ": meanAnomalyDeg must be 0 (the client tree stores mean anomaly 0 at the epoch; move the epoch instead)");
            if (!(o.SemiMajorAxis > 0) || !(o.Eccentricity >= 0) || !(o.Eccentricity < 1))
                throw new WorldFileException(name + ": a body needs an elliptic orbit (semiMajorAxis > 0, 0 <= eccentricity < 1)");
        }

        private static void CheckBarycentricNu(WorldBodyDef body, double mass, double otherMass, List<string> warnings)
        {
            double derived = TrajectoryUtility.GetBarycentricOrbitNu((mass + otherMass) * MassUtility.G, mass, otherMass);
            if (body.Orbit.OrbitNu != null && !Near(body.Orbit.OrbitNu.Value, derived))
                warnings.Add(Label(body) + ": orbitNu " + body.Orbit.OrbitNu.Value.ToString("R", CultureInfo.InvariantCulture) + " differs from G*m_other^3/(m_A+m_B)^2 = " + derived.ToString("R", CultureInfo.InvariantCulture) +
                             "; the client derives it from the masses");
        }

        private static TrajectorySettings OrbitSettings(WorldBodyDef body, List<string> warnings, bool withMass = true)
        {
            WorldOrbitDef o = body.Orbit;
            string name = Label(body);
            return new TrajectorySettings
            {
                mass = withMass ? MassOf(body, warnings) : 0f,
                eccentricity = ToFloat(o.Eccentricity, name, "eccentricity", warnings),
                semiMajorAxis = ToFloat(o.SemiMajorAxis, name, "semiMajorAxis", warnings),
                inclination = ToFloat(o.InclinationDeg, name, "inclinationDeg", warnings),
                argumentOfPeriapsis = ToFloat(o.AopDeg, name, "aopDeg", warnings),
                longitudeAscendingNode = ToFloat(o.LanDeg, name, "lanDeg", warnings),
                timeShift = ToFloat(o.Epoch, name, "epoch", warnings),
            };
        }

        /// <summary>Mass of a body as the client tree stores it: float32 of nu / G.</summary>
        private static float MassOf(WorldBodyDef body, List<string> warnings)
        {
            float mass = (float) (body.Nu / MassUtility.G);
            if (warnings != null && !Near(mass * MassUtility.G, body.Nu))
                warnings.Add(Label(body) + ": nu " + body.Nu.ToString("R", CultureInfo.InvariantCulture) + " is not G times a float32 mass; the client uses " + ((double) mass).ToString("R", CultureInfo.InvariantCulture) + " kg");
            return mass;
        }

        private static float ToFloat(double value, string body, string field, List<string> warnings)
        {
            float result = (float) value;
            if (result != value) warnings.Add(body + ": " + field + " " + value.ToString("R", CultureInfo.InvariantCulture) + " is rounded to float32 " + ((double) result).ToString("R", CultureInfo.InvariantCulture));
            return result;
        }

        private static bool Near(double a, double b)
        {
            return Math.Abs(a - b) <= DerivedTolerance * Math.Max(Math.Abs(a), Math.Abs(b));
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        private static string Label(WorldBodyDef body)
        {
            return string.IsNullOrEmpty(body.Name) || body.Name == body.Id ? body.Id : body.Name + " (" + body.Id + ")";
        }
    }
}
