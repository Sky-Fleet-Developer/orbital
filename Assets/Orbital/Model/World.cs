using System;
using System.Collections.Generic;
using Orbital.Model.Handles;
using Orbital.Model.Serialization;
using Orbital.Model.Services;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEngine;
using Zenject;

namespace Orbital.Model
{
    public class World : MonoBehaviour
    {
       
        [SerializeField] private TreeContainer tree;
        
        private Dictionary<IMass, Transform> _viewsPerMass;
        private Dictionary<IMass, RelativeTrajectory> _trajectories;
        private Dictionary<CelestialSystemComponent, IMass> _massPerCelestial;
        [Inject] private LoopEmitterService _loopEmitterService;
        [Inject] private DiContainer _container;
        public void Load()
        {
            tree.Load();
            _trajectories = new Dictionary<IMass, RelativeTrajectory>();
            _massPerCelestial = new Dictionary<CelestialSystemComponent, IMass>();
            tree.Root.FillTrajectoriesRecursively(_trajectories);
            ReconstructHierarchy(tree.Root);
            InjectHierarchy();
        }

        private void InjectHierarchy()
        {
            foreach (Transform value in _viewsPerMass.Values)
            {
                _container.InjectGameObject(value.gameObject);
            }
        }

        public void Register()
        {
            //_loopEmitterService.Add(this);
        }
        
        #if UNITY_EDITOR
        public void RefreshHierarchy()
        {
            tree.Load();
            ReconstructHierarchy(tree.Root);
        }
        #endif
        
        private void ReconstructHierarchy(IMass mRoot)
        {
            string rootMassName = "RootMass";
            Transform root = transform.Find(rootMassName);
            if (!root)
            {
                root = MakeNewObject(rootMassName, transform, false);
            }

            ReconstructRecursively(mRoot, root);
            
            _viewsPerMass = mRoot.GetMap(root);
            foreach (IMass mass in mRoot.GetRecursively())
            {
                if(mass == null) continue;
                if (_viewsPerMass[mass].TryGetComponent(out CelestialSystemComponent value))
                {
                    _massPerCelestial.TryAdd(value, mass);
                    value.Setup(mass, _trajectories[mass]);   
                }
            }
        }
        
        private void ReconstructRecursively(IMass mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMass mass in mRoot.GetContent())
            {
                i++;
                if(mass == null) continue;
                string wantedName = $"Child[{i}]";
                Transform tChild = tRoot.Find(wantedName);
                if (tChild == null)
                {
                    tChild = MakeNewObject(wantedName, tRoot, mass is CelestialBody);
                }
                ReconstructRecursively(mass, tChild);
            }
        }

        private Transform MakeNewObject(string name, Transform parent, bool isCelestial)
        {
            Transform newObject = new GameObject(name, isCelestial ? new [] {typeof(CelestialSystemComponent)} : new Type[0]).transform;
            newObject.SetParent(parent);
            return newObject;
        }
    }
}