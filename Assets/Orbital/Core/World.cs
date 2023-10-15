using System.Collections.Generic;
using Ara3D;
using Orbital.Core.Simulation;
using Orbital.Core.SystemComponents;
using Orbital.Core.TrajectorySystem;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public class World : MonoBehaviour
    {
        [SerializeField] private TreeContainer tree;
        [Inject] private DiContainer _container;
        
        public void Load()
        {
            tree.Load();
            tree.CalculateForRoot(transform);
            InjectHierarchy();
        }

        public IEnumerable<IRigidBody> GetRigidbodyParents(MassSystemComponent parent)
        {
            return tree._rigidbodyParents[tree._componentPerMass[parent]];
        }
        
        public IEnumerable<IRigidBody> GetRigidbodyParents(IMassSystem parent)
        {
            return tree._rigidbodyParents[parent];
        }

        public void RegisterRigidBody(IRigidBody value)
        {
            tree.AddRigidbody(value);
        }
        
        public DVector3 GetGlobalPosition(MassSystemComponent massSystemComponent)
        {
            return tree.GetGlobalPosition(massSystemComponent, TimeService.WorldTime);
        }

        public DVector3 GetGlobalPosition(IMassSystem massSystem)
        {
            return tree.GetGlobalPosition(massSystem, TimeService.WorldTime);
        }

        private void InjectHierarchy()
        {
            foreach (Transform value in tree._transforms.Values)
            {
                _container.InjectGameObject(value.gameObject);
            }
        }
    }
}