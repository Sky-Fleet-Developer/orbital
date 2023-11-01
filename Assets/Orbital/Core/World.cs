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
            if (tree.IsInitialized)
            {
                if (Application.isPlaying)
                {
                    Debug.LogError("World has been already loaded!");
                }
                return;
            }
            tree.Load();
            tree.CalculateForRoot(transform, this);
            InjectHierarchy();
        }

        public IEnumerable<IDynamicBody> GetChildren(IStaticBody parent)
        {
            foreach (IDynamicBodyAccessor dynamicBodyAccessor in tree._dynamicChildren[tree._massPerComponent[parent]])
            {
                yield return dynamicBodyAccessor.Self;
            }
        }
        
        public IEnumerable<IDynamicBody> GetChildren(IMassSystem parent)
        {
            foreach (IDynamicBodyAccessor dynamicBodyAccessor in tree._dynamicChildren[parent])
            {
                yield return dynamicBodyAccessor.Self;
            }
        }

        public IMassSystem GetParent(StaticBody mass)
        {
            return tree._parents.TryGetValue(tree._massPerComponent[mass], out IMassSystem value) ? value : null;
        }
        
        public IMassSystem GetParent(IMassSystem mass)
        {
            return tree._parents.TryGetValue(mass, out IMassSystem value) ? value : null;
        }

        internal void RegisterRigidBody(IDynamicBodyAccessor value)
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

        public IStaticBody GetParentByWorldPosition(DVector3 worldPosition, double time)
        {
            bool Check(IStaticBodyAccessor body, DVector3 relativePosition)
            {
                return relativePosition.Length() < MassUtility.GetGravityRadius(body.Self.GravParameter);
            }
            
            IEnumerable<IMassSystem> array = tree.Root.GetContent();
            IMassSystem selected = tree.Root;
            DVector3 position = worldPosition;
            while (true)
            {
                IMassSystem next = null;
                foreach (IMassSystem massSystem in array)
                {
                    var body = tree._componentPerMass[massSystem];
                    DVector3 relativePosition = position - body.Trajectory.GetPositionAtT(time);
                    if (Check(body, relativePosition))
                    {
                        position = relativePosition;
                        next = massSystem;
                        break;
                    }
                }

                if (next == null)
                {
                    return tree._componentPerMass[selected].Self;
                }
                else
                {
                    array = next.GetContent();
                }
            }
        }

        private void InjectHierarchy()
        {
            #if UNITY_EDITOR
            if (_container == null)
            {
                if(Application.isPlaying) Debug.LogError("Has no DI container in World component");
                return;
            }
            #endif
            _container.InjectGameObject(gameObject);
            foreach (Transform value in tree._transforms.Values)
            {
                _container.InjectGameObject(value.gameObject);
            }
        }
    }
}