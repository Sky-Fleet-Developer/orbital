using System;
using System.Collections.Generic;
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
        [JsonIgnore] public Dictionary<IMassSystem, IStaticTrajectory> _trajectories { get; private set; }
        [JsonIgnore] internal Dictionary<IStaticBody, IMassSystem> _massPerComponent { get; private set; }
        [JsonIgnore] internal Dictionary<IMassSystem, IStaticBody> _componentPerMass { get; private set; }
        [JsonIgnore] internal Dictionary<IMassSystem, List<IDynamicBodyAccessor>> _children { get; private set; }
        [JsonIgnore] public Dictionary<IMassSystem, IMassSystem> _parents { get; private set; }

        [SerializeField, TextArea(minLines: 6, maxLines: 10)]
        private string serializedValue;

        public bool IsInitialized => _transforms != null && Root != null;

        public void Load()
        {
            ISerializer serializer = new JsonPerformance();
            Root = null;
            serializer.Populate(this, serializedValue);
        }

        public void CalculateForRoot(Transform tRoot)
        {
            CreateCache();
            if (Root == null) return;
            Root.FillTrajectoriesRecursively(_trajectories);
            ReconstructHierarchy(Root, tRoot);
            foreach (IMassSystem massSystem in _transforms.Keys)
            {
                _children.Add(massSystem, new List<IDynamicBodyAccessor>());
            }
        }

        private void CreateCache()
        {
            _trajectories = new Dictionary<IMassSystem, IStaticTrajectory>();
            _massPerComponent = new Dictionary<IStaticBody, IMassSystem>();
            _componentPerMass = new Dictionary<IMassSystem, IStaticBody>();
            _children = new Dictionary<IMassSystem, List<IDynamicBodyAccessor>>();
            _parents = new Dictionary<IMassSystem, IMassSystem>();
        }

        internal void AddRigidbody(IDynamicBodyAccessor component)
        {
            var parent = _massPerComponent[component.Parent];
            List<IDynamicBodyAccessor> list = _children[parent];
            if (!list.Contains(component))
            {
                list.Add(component);
                component.Trajectory = new StaticTrajectory(parent);
            }
        }

        private void ReconstructHierarchy(IMassSystem mRoot, Transform tRoot)
        {
            void SetupMassComponent(IMassSystem child)
            {
                if (child == null) return;
                if (_transforms[child].TryGetComponent(out IStaticBodyAccessor value))
                {
                    _massPerComponent.TryAdd(value.Self, child);
                    _componentPerMass.TryAdd(child, value.Self);
                    if (_trajectories.TryGetValue(child, out IStaticTrajectory trajectory))
                    {
                        if (child != mRoot)
                        {
                            value.Parent = _componentPerMass[mRoot];
                        }

                        value.Trajectory = trajectory;
                        value.MassSystem = child;
                    }
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
            Transform newObject = new GameObject(name, new[] {typeof(StaticBody)}).transform;
            newObject.SetParent(parent);
            return newObject;
        }

        public void FillTrajectories()
        {
            Root.FillTrajectoriesRecursively(_trajectories);
        }
    }
}