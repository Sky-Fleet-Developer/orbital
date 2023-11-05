using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Orbital.Core.Serialization;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Utilities;
using UnityEngine;

namespace Orbital.Core
{
    [Serializable]
    public class TreeContainer
    {
        [JsonProperty] public IMassSystem Root;
        [JsonIgnore] public Dictionary<IMassSystem, Transform> _transforms { get; private set; }
        [JsonIgnore] public Dictionary<IMassSystem, IStaticOrbit> _trajectories { get; private set; }
        [JsonIgnore] internal Dictionary<IStaticBody, IMassSystem> _massPerComponent { get; private set; }
        [JsonIgnore] internal Dictionary<IMassSystem, IStaticBodyAccessor> _componentPerMass { get; private set; }
        [JsonIgnore] internal Dictionary<IMassSystem, List<IDynamicBody>> _dynamicChildren { get; private set; }
        [JsonIgnore] internal Dictionary<IMassSystem, IMassSystem[]> _staticChildren { get; private set; }
        [JsonIgnore] public Dictionary<IMassSystem, IMassSystem> _parents { get; private set; }
        private World _world;
        [SerializeField, TextArea(minLines: 6, maxLines: 10)]
        private string serializedValue;

        public bool IsInitialized => _transforms != null && Root != null;

        public void Load()
        {
            ISerializer serializer = new JsonPerformance();
            Root = null;
            serializer.Populate(this, serializedValue);
        }

        public void CalculateForRoot(Transform tRoot, World world)
        {
            _world = world;
            CreateCache();
            if (Root == null) return;
            Root.FillTrajectoriesRecursively(_trajectories);
            ReconstructHierarchy(Root, tRoot);
            foreach (IMassSystem massSystem in _transforms.Keys)
            {
                _dynamicChildren.Add(massSystem, new List<IDynamicBody>());
            }
        }

        private void CreateCache()
        {
            _trajectories = new Dictionary<IMassSystem, IStaticOrbit>();
            _massPerComponent = new Dictionary<IStaticBody, IMassSystem>();
            _componentPerMass = new Dictionary<IMassSystem, IStaticBodyAccessor>();
            _dynamicChildren = new Dictionary<IMassSystem, List<IDynamicBody>>();
            _staticChildren = new Dictionary<IMassSystem, IMassSystem[]>();
            _parents = new Dictionary<IMassSystem, IMassSystem>();
        }

        internal void AddRigidbody(IDynamicBody component)
        {
            var parent = _massPerComponent[component.Parent];
            List<IDynamicBody> list = _dynamicChildren[parent];
            if (!list.Contains(component))
            {
                list.Add(component);
            }
        }

        private void ReconstructHierarchy(IMassSystem mRoot, Transform tRoot)
        {
            void SetupMassComponent(IMassSystem child)
            {
                if (child == null) return;
                _staticChildren.Add(child, child.GetContent().ToArray());
                if (_transforms[child].TryGetComponent(out IStaticBodyAccessor value))
                {
                    _massPerComponent.TryAdd(value.Self, child);
                    _componentPerMass.TryAdd(child, value);
                    if (child != mRoot)
                    {
                        value.Parent = _componentPerMass[_parents[child]].Self;
                    }
                    if (_trajectories.TryGetValue(child, out IStaticOrbit trajectory))
                    {
                        value.Orbit = trajectory;
                    }
                    value.MassSystem = child;
                    value.World = _world;
                    value.IsSatellite = _parents.TryGetValue(child, out IMassSystem parent) && parent.IsSatellite(child);
                }
            }

            string rootMassName = "RootMass";
            Transform root = tRoot.Find(rootMassName);
            if (!root)
            {
                root = MakeNewObject(rootMassName, tRoot);
            }

            ReconstructRecursively(mRoot, root);

            _transforms = mRoot.GetMap(root);
            SetupMassComponent(mRoot);
            foreach (IMassSystem mass in mRoot.GetRecursively())
            {
                SetupMassComponent(mass);
            }
            foreach (KeyValuePair<IMassSystem, IMassSystem[]> kv in _staticChildren)
            {
                if (_componentPerMass.TryGetValue(kv.Key, out IStaticBodyAccessor component))
                {
                    component.Children = kv.Value
                        .Select(x => _componentPerMass.TryGetValue(x, out IStaticBodyAccessor value) ? value.Self : null)
                        .Where(x => x != null).ToArray();
                }
            }
        }

        private void ReconstructRecursively(IMassSystem mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMassSystem mass in mRoot.GetContent())
            {
                if (mass == null) continue;
                if (!_parents.TryAdd(mass, mRoot))
                {
                    _parents[mass] = mRoot;
                }

                i++;
                Transform tChild = tRoot.FindRegex($".*\\[{i}\\]$");
                if (!tChild)
                {
                    tChild = MakeNewObject($"Child[{i}]", tRoot);
                }

                ReconstructRecursively(mass, tChild);
            }
        }

        private Transform MakeNewObject(string name, Transform parent)
        {
            Transform newObject = new GameObject(name).transform;
            newObject.SetParent(parent);
            return newObject;
        }

        public void FillTrajectories()
        {
            Root.FillTrajectoriesRecursively(_trajectories);
        }
    }
}