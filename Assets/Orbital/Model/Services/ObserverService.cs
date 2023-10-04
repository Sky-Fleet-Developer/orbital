using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.Simulation;
using Orbital.Model.SystemComponents;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Model.Services
{
    public class ObserverService : MonoBehaviour, IUpdateByFrequencyHandler
    {
        [SerializeField] private string simulationSceneName;
        [SerializeField] private float visibleDistance;
        private double _visibleDistanceSqr;
        [Inject] private World _world;
        private List<Observer> _observers;
        private Queue<Observer> _awaitingForScene = new Queue<Observer>();
        private Queue<Scene> _scenesPool = new Queue<Scene>();
        public float VisibleDistance => visibleDistance;
        
        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadSceneAsync(simulationSceneName, LoadSceneMode.Additive);
            _visibleDistanceSqr = visibleDistance * visibleDistance;
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
            _scenesPool.Enqueue(scene);
        }

        public async void RegisterObserver(Observer observer)
        {
            while (_scenesPool.Count == 0)
            {
                await Task.Yield();
            }

            observer.RegisterComplete(_scenesPool.Dequeue());
            SceneManager.LoadSceneAsync(simulationSceneName, LoadSceneMode.Additive);
        }

        UpdateFrequency IUpdateByFrequencyHandler.Frequency => UpdateFrequency.Every100Frame;
        void IUpdateByFrequencyHandler.Update()
        {
            foreach (Observer observer in _observers)
            {
                foreach (RigidBodySystemComponent component in _world.GetRigidbodyParents(observer.Parent))
                {
                    double sqrDistanceInSpace = (component.LocalPosition - observer.LocalPosition).LengthSquared();
                    RigidBodyMode wantedMode = sqrDistanceInSpace < _visibleDistanceSqr
                        ? RigidBodyMode.Simulation
                        : RigidBodyMode.Trajectory;
                    if (component.Mode != wantedMode)
                    {
                        
                    }
                }
            }
        }
    }
}