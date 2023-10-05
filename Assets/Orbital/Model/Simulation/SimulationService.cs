using System.Collections.Generic;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.SystemComponents;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Model.Simulation
{
    public class SimulationService : MonoBehaviour, IObserverTriggerHandler
    {
        [Inject] private ObserverService _observerService;
        private Dictionary<RigidBodySystemComponent, Rigidbody> _presentations = new ();

        [Inject] private void Inject(DiContainer container)
        {
            HandlesRegister.RegisterHandlers(this, container);
        }
        
        private void AssemblePresentation(RigidBodySystemComponent target, Transform root)
        {
            Rigidbody instance = Instantiate(target.Settings.dynamicPresentation, root);
            _presentations.Add(target, instance);
            FromTrajectoryToSimulation(target, instance);
        }

        private void DisassemblePresentation(RigidBodySystemComponent target)
        {
            Destroy(_presentations[target]);
            _presentations.Remove(target);
        }

        private void FromTrajectoryToSimulation(RigidBodySystemComponent component, Rigidbody presentation)
        {
            component.Present();
            Observer observer = _observerService.GetObserverFor(component);
            DVector3 origin = observer.LocalPosition;
            DVector3 originVelocity = observer.LocalVelocity;
            presentation.transform.localPosition = component.LocalPosition - origin;
            presentation.velocity = component.LocalVelocity - originVelocity;
        }
        
        public void OnRigidbodyEnter(RigidBodySystemComponent component, Observer observer)
        {
            AssemblePresentation(component, observer.Root);
        }

        public void OnRigidbodyExit(RigidBodySystemComponent component, Observer observer)
        {
            DisassemblePresentation(component);
        }
    }
}