using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.SystemComponents;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Model.Simulation
{
    public class ObserverService : MonoBehaviour, IUpdateByFrequencyHandler
    {
        [SerializeField] private string simulationSceneName;
        [SerializeField] private float visibleDistance;
        private double _visibleDistanceSqr;
        [Inject] private World _world;
        [Inject] private DiContainer _diContainer;
        private HandlerCollection<IObserverTriggerHandler> _triggerSubscribers;
        private readonly Dictionary<Observer, Scene> _scenes = new ();
        private readonly Dictionary<Observer, Transform> _roots = new ();
        private readonly Queue<Scene> _scenesPool = new ();
        private readonly Dictionary<Observer, HashSet<IRigidBody>> _observingObjects = new();
        private readonly Dictionary<IRigidBody, Observer> _observingObjectPerObserver = new();
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
            _triggerSubscribers = HandlerCollection.GetOrCreateCollection<IObserverTriggerHandler>(_diContainer);
        }

        public Transform GetRootFor(Observer observer)
        {
            return _roots[observer];
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

        public async void RegisterObserver(Observer observer)
        {
            while (_scenesPool.Count == 0)
            {
                await Task.Yield();
            }

            Scene scene = _scenesPool.Dequeue();
            _roots.Add(observer, scene.GetRootGameObjects()[0].transform);
            _scenes.Add(observer, scene);
            _observingObjects.Add(observer, new HashSet<IRigidBody>());
            observer.RegisterComplete(scene);
            SceneManager.LoadSceneAsync(simulationSceneName, LoadSceneMode.Additive); //prepare next scene
        }
        
        public void UnregisterObserver(Observer observer)
        {
            IRigidBody[] array = _observingObjects[observer].ToArray();
            foreach (IRigidBody rigidBodySystemComponent in array)
            {
                RigidbodyLeavesObserver(rigidBodySystemComponent, observer);
            }

            SceneManager.UnloadSceneAsync(_scenes[observer]);
            _scenes.Remove(observer);
            _roots.Remove(observer);
        }

        public Observer GetObserverFor(IRigidBody target)
        {
            return _observingObjectPerObserver[target];
        }
        
        public bool IsInSimulation(IRigidBody target)
        {
            return target.Mode != RigidBodyMode.Trajectory;
        }

        UpdateFrequency IUpdateByFrequencyHandler.Frequency => UpdateFrequency.Every100Frame;
        void IUpdateByFrequencyHandler.Update()
        {
            foreach (Observer observer in _scenes.Keys)
            {
                foreach (IRigidBody component in _world.GetRigidbodyParents(observer.Parent))
                {
                    double sqrDistanceInSpace = (component.LocalPosition - observer.LocalPosition).LengthSquared();
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
        private void RigidbodyEntersObserver(IRigidBody component, Observer observer)
        {
            if (_observingObjectPerObserver.ContainsKey(component))
            {
                _observingObjectPerObserver[component] = observer;
            }
            else
            {
                _observingObjectPerObserver.Add(component, observer);
            }

            _observingObjects[observer].Add(component);
            foreach (IObserverTriggerHandler subscriber in _triggerSubscribers.All())
            {
                subscriber.OnRigidbodyEnter(component, observer);
            }
        }
        private void RigidbodyLeavesObserver(IRigidBody component, Observer observer)
        {
            _observingObjectPerObserver[component] = null;
            _observingObjects[observer].Remove(component);
            foreach (IObserverTriggerHandler observerTriggerHandler in _triggerSubscribers.All())
            {
                observerTriggerHandler.OnRigidbodyExit(component, observer);
            }
        }
    }

}