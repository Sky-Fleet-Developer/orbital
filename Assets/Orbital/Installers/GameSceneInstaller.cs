using Orbital.Model;
using Orbital.Model.Services;
using Orbital.Model.SystemComponents;
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
            Container.BindInstance(GetComponentInChildren<LoopEmitterService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<ComponentsRegistrationService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<TimeService>()).AsSingle();
            World world = Instantiate(worldPrefab);
            Container.Bind<World>().FromInstance(world).AsSingle();
            Container.InjectGameObject(world.gameObject);
            world.Load();
            world.Register();
        }
    }
}