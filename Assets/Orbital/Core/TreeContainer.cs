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
        [JsonIgnore] public Dictionary<IMassSystem, RelativeTrajectory> _trajectories { get; private set; }
        [JsonIgnore] public Dictionary<MassSystemComponent, IMassSystem> _componentPerMass { get; private set; }
        [JsonIgnore] public Dictionary<IMassSystem, List<IRigidBody>> _rigidbodyParents { get; private set; }
        [JsonIgnore] public Dictionary<IMassSystem, IMassSystem> _parents { get; private set; }
        [SerializeField, TextArea(minLines: 6, maxLines: 10)] private string serializedValue;

        public void Load()
        {
            ISerializer serializer = new JsonPerformance();
            Root = null;
            serializer.Populate(this, serializedValue);
        }

        public void CalculateForRoot(Transform tRoot)
        {
            CreateCache();
            if(Root == null) return;
            Root.FillTrajectoriesRecursively(_trajectories);
            ReconstructHierarchy(Root, tRoot);
            foreach (IMassSystem massSystem in _transforms.Keys)
            {
                _rigidbodyParents.Add(massSystem, new List<IRigidBody>());
            }
        }
        
        private void CreateCache()
        {
            _trajectories = new Dictionary<IMassSystem, RelativeTrajectory>();
            _componentPerMass = new Dictionary<MassSystemComponent, IMassSystem>();
            _rigidbodyParents = new Dictionary<IMassSystem, List<IRigidBody>>();
            _parents = new Dictionary<IMassSystem, IMassSystem>();
        }

        public void AddRigidbody(IRigidBody component)
        {
            _rigidbodyParents[_componentPerMass[component.Parent]].Add(component);
        }
        
        private void ReconstructHierarchy(IMassSystem mRoot, Transform tRoot)
        {
            void SetupMassComponent(IMassSystem mass)
            {
                if (mass == null) return;
                if (_transforms[mass].TryGetComponent(out MassSystemComponent value))
                {
                    _componentPerMass.TryAdd(value, mass);
                    if (_trajectories.TryGetValue(mass, out RelativeTrajectory trajectory))
                    {
                        value.Setup(mass, trajectory);
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
                if(mass == null) continue;
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
            Transform newObject = new GameObject(name, new [] {typeof(MassSystemComponent)}).transform;
            newObject.SetParent(parent);
            return newObject;
        }

        public void FillTrajectories()
        {
            Root.FillTrajectoriesRecursively(_trajectories);
        }
    }
}
