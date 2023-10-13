using System.Collections.Generic;
using Ara3D;
using Orbital.Model.Simulation;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEngine;
using Zenject;

namespace Orbital.Model
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

        public void RegisterRigidBody(RigidBodySystemComponent value, out RelativeTrajectory trajectory)
        {
            tree.AddRigidbody(value, out IMassSystem parent);
            trajectory = new RelativeTrajectory(value, parent, SystemType.RigidBody);
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