using System;
using System.Collections.Generic;
using Ara3D;
using Ara3D.Double;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// One body of the world as plain numbers (stage 3.1): what <see cref="BodyFrameTable.Build"/> needs. The client fills it from the world file
    /// (<c>Orbital.Core.Worlds.WorldFrames</c>), the server from its world; both give the same table. Angles in degrees as in the world file.
    /// </summary>
    public struct BodyFrameSource
    {
        public string Id;
        /// <summary>Index of the parent in the source list (it must come before the child); -1 for the root.</summary>
        public int Parent;
        public double Nu;
        public double Radius;
        /// <summary>Override of the sphere of influence radius (the world file's soiRadius); NaN = the Laplace sphere.</summary>
        public double SoiOverride;
        public bool Virtual;
        public double InclinationDeg, Eccentricity, SemiMajorAxis, LanDeg, AopDeg, MeanAnomalyDeg;
        /// <summary>Orbit epoch relative to the world zero epoch (orbit.epoch of the file).</summary>
        public double Epoch;
        /// <summary>Gravitational parameter of the orbit; NaN = the parent's nu (members of a double system have their own).</summary>
        public double OrbitNu;
        public RotationParams Rotation;
    }

    /// <summary>A body in the table: its place in the tree, gravity, sphere of influence, orbit around the parent and surface.</summary>
    public struct BodyFrame
    {
        public int Parent;        // -1 for the root
        public double Nu;
        public double Radius;
        public double SoiRadius;  // +infinity for the root
        public bool Virtual;      // barycenter of a double system: no surface
        public StaticOrbit Orbit; // around the parent; unset for the root
        public SurfaceData Surface;
    }

    /// <summary>
    /// The bodies of the world in one flat table (stage 3.1, engine-free, shared with the server): body frames, local and global states at a game
    /// time, conversion of a craft state between the frames of two bodies. Built once; the queries do not allocate.
    /// Frames: a body's frame is centered on the body with the inertial axes of the world (the same axes in every frame); the global frame is the
    /// root's.
    /// </summary>
    public sealed class BodyFrameTable
    {
        private readonly BodyFrame[] _bodies;
        private readonly string[] _ids;
        private readonly int[][] _children;
        private readonly Dictionary<string, int> _index;

        private BodyFrameTable(BodyFrame[] bodies, string[] ids, int[][] children)
        {
            _bodies = bodies;
            _ids = ids;
            _children = children;
            _index = new Dictionary<string, int>(ids.Length);
            for (int i = 0; i < ids.Length; i++) _index[ids[i]] = i;
        }

        public int Count => _bodies.Length;
        public string IdOf(int i) => _ids[i];
        public ref readonly BodyFrame this[int i] => ref _bodies[i];
        public int[] ChildrenOf(int i) => _children[i];

        /// <summary>Index of the body with this id, -1 if there is none.</summary>
        public int IndexOf(string id) => id != null && _index.TryGetValue(id, out int i) ? i : -1;

        /// <summary>
        /// Builds the table with the rules of the server world (OrbitServer/World.cs, ServerWorld.Build): orbit epoch = epochZero + epoch, orbit nu
        /// default the parent's nu, the sphere of influence by <see cref="TrajectoryUtility.GetSphereOfInfluenceRadius"/> (the radius * k minimum),
        /// the surface by <see cref="SurfaceBuilder"/>.
        /// </summary>
        public static BodyFrameTable Build(IReadOnlyList<BodyFrameSource> sources, double epochZero,
            double minRadiusFactor = TrajectoryUtility.DefaultMinRadiusFactor)
        {
            if (sources == null || sources.Count == 0) throw new ArgumentException("no bodies", nameof(sources));
            const double D2R = Math.PI / 180.0;
            int n = sources.Count;
            BodyFrame[] bodies = new BodyFrame[n];
            string[] ids = new string[n];
            List<int>[] children = new List<int>[n];
            bool root = false;
            for (int i = 0; i < n; i++)
            {
                BodyFrameSource src = sources[i];
                children[i] = new List<int>();
                ids[i] = src.Id ?? throw new ArgumentException($"body {i} has no id", nameof(sources));
                BodyFrame b = default;
                b.Parent = src.Parent;
                b.Nu = src.Nu;
                b.Radius = src.Radius;
                b.Virtual = src.Virtual;
                OrbitFrame frame = default;
                if (src.Parent < 0)
                {
                    if (root) throw new ArgumentException("several root bodies", nameof(sources));
                    root = true;
                    b.SoiRadius = double.PositiveInfinity;
                }
                else
                {
                    if (src.Parent >= i) throw new ArgumentException($"the parent of {src.Id} must come before it", nameof(sources));
                    double parentNu = bodies[src.Parent].Nu;
                    b.Orbit = new StaticOrbit {Nu = double.IsNaN(src.OrbitNu) ? parentNu : src.OrbitNu};
                    b.Orbit.SetOrbit(src.InclinationDeg * D2R, src.Eccentricity, src.SemiMajorAxis, src.LanDeg * D2R, src.AopDeg * D2R,
                        src.MeanAnomalyDeg * D2R, epochZero + src.Epoch);
                    b.SoiRadius = double.IsNaN(src.SoiOverride)
                        ? TrajectoryUtility.GetSphereOfInfluenceRadius(src.SemiMajorAxis, src.Nu, parentNu, src.Radius, minRadiusFactor)
                        : src.SoiOverride;
                    children[src.Parent].Add(i);
                    DMatrix4x4 m = b.Orbit.RotationMatrix;
                    frame.HasParent = true;
                    frame.Normal = ToVec(m.Up());
                    frame.Forward = ToVec(m.Forward());
                    frame.Right = ToVec(m.Right());
                    frame.MeanMotion = b.Orbit.MeanMotion;
                    frame.PositionAtEpochZero = ToVec(b.Orbit.GetSample(epochZero, true, false).position);
                }
                b.Surface = SurfaceBuilder.Build(src.Radius, src.Rotation, frame, epochZero);
                bodies[i] = b;
            }
            if (!root) throw new ArgumentException("no root body", nameof(sources));
            int[][] childArrays = new int[n][];
            for (int i = 0; i < n; i++) childArrays[i] = children[i].ToArray();
            return new BodyFrameTable(bodies, ids, childArrays);
        }

        /// <summary>Position and velocity of body <paramref name="i"/> relative to its parent at game time t (zero for the root).</summary>
        public void LocalState(int i, double t, out Vec3 pos, out Vec3 vel)
        {
            if (_bodies[i].Parent < 0)
            {
                pos = Vec3.Zero;
                vel = Vec3.Zero;
                return;
            }
            (DVector3 p, DVector3 v) = _bodies[i].Orbit.GetSample(t);
            pos = ToVec(p);
            vel = ToVec(v);
        }

        /// <summary>Position and velocity of body <paramref name="i"/> in the global (root) frame: the sum of the local states up the tree.</summary>
        public void GlobalState(int i, double t, out Vec3 pos, out Vec3 vel)
        {
            pos = Vec3.Zero;
            vel = Vec3.Zero;
            for (int b = i; b >= 0 && _bodies[b].Parent >= 0; b = _bodies[b].Parent)
            {
                LocalState(b, t, out Vec3 p, out Vec3 v);
                pos = pos + p;
                vel = vel + v;
            }
        }

        /// <summary>Converts a state from the frame of body <paramref name="from"/> into the frame of body <paramref name="to"/> at time t.</summary>
        public void Convert(int from, int to, double t, ref Vec3 pos, ref Vec3 vel)
        {
            if (from == to) return;
            // through the nearest common ancestor: fewer terms, less rounding than through the root
            int a = from, b = to;
            int da = Depth(a), db = Depth(b);
            while (da > db)
            {
                LocalState(a, t, out Vec3 p, out Vec3 v);
                pos = pos + p;
                vel = vel + v;
                a = _bodies[a].Parent;
                da--;
            }
            Vec3 downPos = Vec3.Zero, downVel = Vec3.Zero;
            while (db > da)
            {
                LocalState(b, t, out Vec3 p, out Vec3 v);
                downPos = downPos + p;
                downVel = downVel + v;
                b = _bodies[b].Parent;
                db--;
            }
            while (a != b)
            {
                LocalState(a, t, out Vec3 pa, out Vec3 va);
                pos = pos + pa;
                vel = vel + va;
                LocalState(b, t, out Vec3 pb, out Vec3 vb);
                downPos = downPos + pb;
                downVel = downVel + vb;
                a = _bodies[a].Parent;
                b = _bodies[b].Parent;
            }
            pos = pos - downPos;
            vel = vel - downVel;
        }

        /// <summary>
        /// The body part of the flight parameters for a craft around body <paramref name="i"/> (stage 3.2): its gravity and its ground. The
        /// craft part and the landing limit stay.
        /// </summary>
        public void ApplyTo(int i, ref FlightParams p)
        {
            p.Nu = _bodies[i].Nu;
            p.Surface = _bodies[i].Surface;
        }

        public int Depth(int i)
        {
            int d = 0;
            for (int b = _bodies[i].Parent; b >= 0; b = _bodies[b].Parent) d++;
            return d;
        }

        private static Vec3 ToVec(DVector3 v) => new Vec3(v.x, v.y, v.z);
    }

    /// <summary>
    /// The rule of the spheres of influence (stage 3.1), one for the client and the server: a craft enters a child's sphere when it is closer than
    /// <see cref="EntryFactor"/> * its radius and leaves its parent's sphere when it is farther than <see cref="ExitFactor"/> * the radius. The gap
    /// between the two keeps a craft that skims the border from switching back and forth. The server finds the same crossings analytically on a
    /// ballistic orbit (OrbitServer/Propagator.cs); the client checks the rule after its kernel steps.
    /// </summary>
    public static class SoiRule
    {
        public const double EntryFactor = 1.0;
        public const double ExitFactor = 1.005;
        private const int MaxChanges = 16; // deeper than any world tree; guards a malformed table

        /// <summary>
        /// Applies the rule to a craft at <paramref name="pos"/>/<paramref name="vel"/> in the frame of <paramref name="parent"/> at time t: leaves
        /// to the grandparent while outside, then enters the children while inside one (nested spheres: several steps at once). On a change the
        /// state is converted into the new frame. Returns true when the parent changed.
        /// </summary>
        public static bool Check(BodyFrameTable table, ref int parent, double t, ref Vec3 pos, ref Vec3 vel)
        {
            bool changed = false;
            for (int guard = 0; guard < MaxChanges; guard++)
            {
                ref readonly BodyFrame body = ref table[parent];
                if (body.Parent >= 0 && Vec3.Length(pos) > body.SoiRadius * ExitFactor)
                {
                    table.LocalState(parent, t, out Vec3 p, out Vec3 v);
                    pos = pos + p;
                    vel = vel + v;
                    parent = body.Parent;
                    changed = true;
                    continue;
                }
                int entered = -1;
                int[] children = table.ChildrenOf(parent);
                for (int k = 0; k < children.Length; k++)
                {
                    int c = children[k];
                    table.LocalState(c, t, out Vec3 cp, out _);
                    if (Vec3.Length(pos - cp) < table[c].SoiRadius * EntryFactor)
                    {
                        entered = c;
                        break;
                    }
                }
                if (entered < 0) break;
                table.LocalState(entered, t, out Vec3 ep, out Vec3 ev);
                pos = pos - ep;
                vel = vel - ev;
                parent = entered;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// The rule after a kernel step that ended at <paramref name="t"/> (stage 3.2): only in flight (a landed craft or a wreck belongs to
        /// its body); on a change the state moves into the new frame and <paramref name="p"/> takes the new body's gravity and ground. The
        /// attitude stays: all frames share the inertial axes. Returns true when the parent changed.
        /// </summary>
        public static bool Step(BodyFrameTable table, ref int parent, ref FlightState s, ref FlightParams p, double t)
        {
            if (table == null || parent < 0 || s.Mode != FlightMode.Flying) return false;
            if (!Check(table, ref parent, t, ref s.Position, ref s.Velocity)) return false;
            table.ApplyTo(parent, ref p);
            return true;
        }
    }
}
