using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Core.Simulation;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] private World worldPrefab;
        //[SerializeField] private RigidBodySystemComponent 
        private World _worldInstance;
        public override void InstallBindings()
        {
            _worldInstance = Instantiate(worldPrefab);
            Container.Bind<World>().FromInstance(_worldInstance).AsSingle();
            Container.BindInstance(GetComponentInChildren<EventLoopEmitterService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<ComponentsRegistrationService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<TimeService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<SimulationService>()).AsSingle();
        }


        public void OnPostInstall()
        {
            Container.InjectGameObject(_worldInstance.gameObject);
            _worldInstance.Load();
        }
    }
}