using Orbital.Model;
using Orbital.Model.Services;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] private World worldPrefab;
        public override void InstallBindings()
        {
            Container.BindInstance(GetComponentInChildren<LoopEmitterService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<ComponentsRegistrationService>()).AsSingle();
            World world = Container.InstantiatePrefab(worldPrefab).GetComponent<World>();
            Container.Bind<World>().FromInstance(world).AsSingle();
            world.Load();
            world.Register();
        }
    }
}