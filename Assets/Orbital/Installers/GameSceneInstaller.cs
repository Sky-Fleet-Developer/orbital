using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Core.Simulation;
using Orbital.Core.SystemComponents;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] private World worldPrefab;
        //[SerializeField] private RigidBodySystemComponent 
        public override void InstallBindings()
        {
            World world = Instantiate(worldPrefab);
            Container.Bind<World>().FromInstance(world).AsSingle();
            Container.BindInstance(GetComponentInChildren<EventLoopEmitterService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<ComponentsRegistrationService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<TimeService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<ObserverService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<SimulationService>()).AsSingle();
            Container.InjectGameObject(world.gameObject);
            world.Load();
        }
    }
}