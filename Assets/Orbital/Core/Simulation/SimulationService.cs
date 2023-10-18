using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Core.Handles;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Core.Simulation
{
    public class SimulationService : MonoBehaviour, IUpdateByFrequencyHandler, ISimulationSpaceTriggerHandler
    {
        [SerializeField] private string simulationSceneName;
        [SerializeField] private float visibleDistance;
        private double _visibleDistanceSqr;
        [Inject] private World _world;
        [Inject] private DiContainer _diContainer;
        private HandlerCollection<ISimulationSpaceTriggerHandler> _triggerSubscribers;
        private readonly Dictionary<SimulationSpace, Scene> _scenes = new ();
        private readonly Dictionary<SimulationSpace, Transform> _roots = new ();
        private readonly Queue<Scene> _scenesPool = new ();
        private readonly Dictionary<SimulationSpace, HashSet<IDynamicBody>> _simulationObjects = new();
        private readonly Dictionary<IDynamicBody, SimulationSpace> _simulationObjectPerSimulation = new();
        public float VisibleDistance => visibleDistance;
        
        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadSceneAsync(simulationSceneName, LoadSceneMode.Additive);
            _visibleDistanceSqr = visibleDistance * visibleDistance;
        }

        private void Start()
        {
            HandlesRegister.RegisterHandlers(this, _diContainer);
            _triggerSubscribers = HandlerCollection.GetOrCreateCollection<ISimulationSpaceTriggerHandler>(_diContainer);
        }

        public Transform GetRootFor(SimulationSpace simulationSpace)
        {
            return _roots[simulationSpace];
        }

        private void OnValidate()
        {
            _visibleDistanceSqr = visibleDistance * visibleDistance;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex != SceneManager.GetSceneByName(simulationSceneName).buildIndex) return;
            _scenesPool.Enqueue(scene);
        }

        public async void RegisterSimulation(SimulationSpace simulationSpace)
        {
            while (_scenesPool.Count == 0)
            {
                await Task.Yield();
            }

            Scene scene = _scenesPool.Dequeue();
            _roots.Add(simulationSpace, scene.GetRootGameObjects()[0].transform);
            _scenes.Add(simulationSpace, scene);
            _simulationObjects.Add(simulationSpace, new HashSet<IDynamicBody>());
            simulationSpace.RegisterComplete(scene);
            SceneManager.LoadSceneAsync(simulationSceneName, LoadSceneMode.Additive); //prepare next scene
        }
        
        public void UnregisterSimulation(SimulationSpace simulationSpace)
        {
            IDynamicBody[] array = _simulationObjects[simulationSpace].ToArray();
            foreach (IDynamicBody rigidBodySystemComponent in array)
            {
                RigidbodyLeavesObserver(rigidBodySystemComponent, simulationSpace);
            }

            SceneManager.UnloadSceneAsync(_scenes[simulationSpace]);
            _scenes.Remove(simulationSpace);
            _roots.Remove(simulationSpace);
        }

        public SimulationSpace GetObserverFor(IDynamicBody target)
        {
            return _simulationObjectPerSimulation[target];
        }
        
        public bool IsInSimulation(IDynamicBody target)
        {
            return target.Mode != DynamicBodyMode.Trajectory;
        }

        UpdateFrequency IUpdateByFrequencyHandler.Frequency => UpdateFrequency.Every100Frame;
        void IUpdateByFrequencyHandler.Update()
        {
            foreach (SimulationSpace observer in _scenes.Keys)
            {
                foreach (IDynamicBody component in _world.GetChildren(observer.Parent))
                {
                    double sqrDistanceInSpace = (component.TrajectorySampler.GetSample(TimeService.WorldTime).position - observer.Position).LengthSquared();
                    bool isInSimulation = IsInSimulation(component);
                    bool distanceCompare = sqrDistanceInSpace < _visibleDistanceSqr;
                    
                    if (distanceCompare != isInSimulation)
                    {
                        switch (distanceCompare)
                        {
                            case true:
                                RigidbodyEntersObserver(component, observer);
                                break;
                            case false:
                                RigidbodyLeavesObserver(component, observer);
                                break;
                        }
                    }
                }
            }
        }
        
        public void OnRigidbodyEnter(IDynamicBody body, SimulationSpace simulationSpace)
        {
            body.Present(simulationSpace);
        }

        public void OnRigidbodyExit(IDynamicBody body, SimulationSpace simulationSpace)
        {
            body.RemovePresent();
        }
        
        private void RigidbodyEntersObserver(IDynamicBody component, SimulationSpace simulationSpace)
        {
            if (_simulationObjectPerSimulation.ContainsKey(component))
            {
                _simulationObjectPerSimulation[component] = simulationSpace;
            }
            else
            {
                _simulationObjectPerSimulation.Add(component, simulationSpace);
            }

            _simulationObjects[simulationSpace].Add(component);
            foreach (ISimulationSpaceTriggerHandler subscriber in _triggerSubscribers.All())
            {
                subscriber.OnRigidbodyEnter(component, simulationSpace);
            }
        }
        private void RigidbodyLeavesObserver(IDynamicBody component, SimulationSpace simulationSpace)
        {
            _simulationObjectPerSimulation[component] = null;
            _simulationObjects[simulationSpace].Remove(component);
            foreach (ISimulationSpaceTriggerHandler observerTriggerHandler in _triggerSubscribers.All())
            {
                observerTriggerHandler.OnRigidbodyExit(component, simulationSpace);
            }
        }
    }

}