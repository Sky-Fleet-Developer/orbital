using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core
{
    /// <summary>
    /// State of the world without any scene objects: the tree of masses, their static orbits and the registered dynamic bodies.
    /// Time never comes from a global: every time-dependent member takes it as an argument.
    /// </summary>
    public class WorldModel
    {
        private readonly Dictionary<IMassSystem, StaticOrbit> _trajectories = new();
        private readonly Dictionary<IMassSystem, IMassSystem> _parents = new();
        private readonly Dictionary<IMassSystem, IMassSystem[]> _staticChildren = new();
        private readonly Dictionary<IStaticBody, IMassSystem> _massPerBody = new();
        private readonly Dictionary<IMassSystem, IStaticBodyAccessor> _bodyPerMass = new();
        private readonly List<IDynamicBody> _dynamicBodies = new();
        // Parents always precede their children.
        private readonly List<IMassSystem> _masses = new();

        public IMassSystem Root { get; }
        public IReadOnlyDictionary<IMassSystem, StaticOrbit> Trajectories => _trajectories;
        public IReadOnlyList<IDynamicBody> DynamicBodies => _dynamicBodies;

        public event Action<IDynamicBody> DynamicBodyRegistered;

        public WorldModel(IMassSystem root, double time)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            AddRecursively(root);
            RefreshTrajectories(time);
        }

        /// <summary>Recalculates every static orbit for <paramref name="time"/>.</summary>
        public void RefreshTrajectories(double time)
        {
            Root.FillTrajectoriesRecursively(_trajectories, time);
        }

        /// <summary>
        /// Connects each mass with the body that represents it and lets the body know its place in the tree.
        /// <paramref name="resolve"/> returns null for a mass that has no body.
        /// </summary>
        public void BindBodies(Func<IMassSystem, IStaticBodyAccessor> resolve)
        {
            _massPerBody.Clear();
            _bodyPerMass.Clear();
            foreach (IMassSystem mass in _masses)
            {
                IStaticBodyAccessor body = resolve(mass);
                if (body == null) continue;
                _massPerBody.TryAdd(body.Self, mass);
                _bodyPerMass.TryAdd(mass, body);
                if (mass != Root && _parents.TryGetValue(mass, out IMassSystem parent) && _bodyPerMass.TryGetValue(parent, out IStaticBodyAccessor parentBody))
                {
                    body.Parent = parentBody.Self;
                }
                if (_trajectories.TryGetValue(mass, out StaticOrbit orbit))
                {
                    body.Orbit = orbit;
                }
                body.MassSystem = mass;
                body.IsSatellite = _parents.TryGetValue(mass, out IMassSystem owner) && owner.IsSatellite(mass);
            }

            foreach (KeyValuePair<IMassSystem, IMassSystem[]> pair in _staticChildren)
            {
                if (!_bodyPerMass.TryGetValue(pair.Key, out IStaticBodyAccessor body)) continue;
                body.Children = pair.Value
                    .Select(x => x != null && _bodyPerMass.TryGetValue(x, out IStaticBodyAccessor child) ? child.Self : null)
                    .Where(x => x != null)
                    .ToArray();
            }
        }

        public bool IsBound => _bodyPerMass.ContainsKey(Root);

        public IStaticBody GetRootBody()
        {
            return _bodyPerMass.TryGetValue(Root, out IStaticBodyAccessor body) ? body.Self : null;
        }

        public IMassSystem GetParent(IMassSystem mass)
        {
            return _parents.TryGetValue(mass, out IMassSystem value) ? value : null;
        }

        public IMassSystem GetParent(IStaticBody body)
        {
            return GetParent(_massPerBody[body]);
        }

        public DVector3 GetGlobalPosition(IMassSystem mass, double time)
        {
            DVector3 result = DVector3.Zero;
            while (mass != null && mass != Root)
            {
                result += _trajectories[mass].GetPositionAtT(time);
                _parents.TryGetValue(mass, out mass);
            }
            return result;
        }

        public DVector3 GetGlobalPosition(IStaticBody body, double time)
        {
            return GetGlobalPosition(_massPerBody[body], time);
        }

        /// <summary>Descends the tree, at each level taking the first mass whose gravity sphere contains <paramref name="worldPosition"/>.</summary>
        public IStaticBody GetParentByWorldPosition(DVector3 worldPosition, double time)
        {
            IEnumerable<IMassSystem> candidates = Root.GetContent();
            IMassSystem selected = Root;
            DVector3 position = worldPosition;
            while (true)
            {
                IMassSystem next = null;
                foreach (IMassSystem mass in candidates)
                {
                    if (mass == null || !_bodyPerMass.TryGetValue(mass, out IStaticBodyAccessor body)) continue;
                    DVector3 relativePosition = position - body.Orbit.GetPositionAtT(time);
                    if (relativePosition.Length() < MassUtility.GetGravityRadius(body.Self.GravParameter))
                    {
                        position = relativePosition;
                        next = mass;
                        break;
                    }
                }

                if (next == null)
                {
                    return _bodyPerMass[selected].Self;
                }
                selected = next;
                candidates = next.GetContent();
            }
        }

        public void RegisterDynamicBody(IDynamicBody body)
        {
            if (_dynamicBodies.Contains(body)) return;
            _dynamicBodies.Add(body);
            DynamicBodyRegistered?.Invoke(body);
        }

        private void AddRecursively(IMassSystem mass)
        {
            _masses.Add(mass);
            _staticChildren[mass] = mass.GetContent().ToArray();
            foreach (IMassSystem child in _staticChildren[mass])
            {
                if (child == null) continue;
                _parents[child] = mass;
                AddRecursively(child);
            }
        }
    }
}
