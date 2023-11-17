using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public class World : MonoBehaviour
    {
        [SerializeField] private TreeContainer tree;
        [Inject] private DiContainer _container;

        public event Action<IDynamicBody> DynamicBodyRegisterHandler;
        public event Action<IDynamicBody> DynamicBodyUnregisterHandler;
        
        public void Load(bool putOrbitsFromTree = true)
        {
            if (tree != null)
            {
                if (tree.IsInitialized)
                {
                    if (Application.isPlaying)
                    {
                        Debug.LogError("PlayerCharacter has been already loaded!");
                    }
                    return;
                }

                if (putOrbitsFromTree) tree.Load();
                tree.CalculateForRoot(transform, this);
            }

            IStaticBodyAccessor root = GetComponentInChildren<IStaticBodyAccessor>();
            InitHierarchyRecursively(root, ((MonoBehaviour) root).transform);
            InjectHierarchy();
        }

        public void InitHierarchyRecursively(IStaticBodyAccessor bRoot, Transform tRoot)
        {
            List<IStaticBody> children = new List<IStaticBody>();
            foreach (Transform child in tRoot)
            {
                IStaticBodyAccessor staticBodyAccessor = child.GetComponent<IStaticBodyAccessor>();
                if(staticBodyAccessor == null) continue;
                children.Add(staticBodyAccessor.Self);
                staticBodyAccessor.Parent = bRoot.Self;
                InitHierarchyRecursively(staticBodyAccessor, child);
            }

            bRoot.Children = children.ToArray();
        }

        public void RegisterRigidBody(IDynamicBody value)
        {
           // tree.AddRigidbody(value);
            DynamicBodyRegisterHandler?.Invoke(value);
        }
        
        private void InjectHierarchy()
        {
            #if UNITY_EDITOR
            if (_container == null)
            {
                if(Application.isPlaying) Debug.LogError("Has no DI container in PlayerCharacter component");
                return;
            }
            #endif
            foreach (Transform tr in gameObject.GetComponentsInChildren<Transform>())
            {
                _container.InjectGameObject(tr.gameObject);
            }
        }

        public IStaticBody GetRootBody()
        {
            return tree._componentPerMass[tree.Root].Self;
        }
    }
}