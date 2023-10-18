using System.Collections.Generic;
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
        
        public void Load()
        {
            tree.Load();
            tree.CalculateForRoot(transform);
            InjectHierarchy();
        }

        public IEnumerable<IDynamicBody> GetChildren(IStaticBody parent)
        {
            return tree._children[tree._massPerComponent[parent]];
        }
        
        public IEnumerable<IDynamicBody> GetChildren(IMassSystem parent)
        {
            return tree._children[parent];
        }

        public IMassSystem GetParent(StaticBody mass)
        {
            return tree._parents.TryGetValue(tree._massPerComponent[mass], out IMassSystem value) ? value : null;
        }
        
        public IMassSystem GetParent(IMassSystem mass)
        {
            return tree._parents.TryGetValue(mass, out IMassSystem value) ? value : null;
        }

        public void RegisterRigidBody(IDynamicBody value)
        {
            tree.AddRigidbody(value);
        }
        
        public DVector3 GetGlobalPosition(IStaticBody staticBody)
        {
            return tree.GetGlobalPosition(staticBody, TimeService.WorldTime);
        }

        public DVector3 GetGlobalPosition(IMassSystem massSystem)
        {
            return tree.GetGlobalPosition(massSystem, TimeService.WorldTime);
        }

        private void InjectHierarchy()
        {
            _container.InjectGameObject(gameObject);
            foreach (Transform value in tree._transforms.Values)
            {
                _container.InjectGameObject(value.gameObject);
            }
        }
    }
}