using Orbital.Model;
using Sirenix.OdinInspector;
using Zenject;

namespace Orbital.Installers
{
    public class WorldInstaller : MonoInstaller
    {
        [Inject] private IFactory<World> _worldFactory;
        [ShowInInspector] private World _world;

        public override void InstallBindings()
        {
            _world = _worldFactory.Create();
            Container.Inject(_world);
            Container.Bind<World>().FromInstance(_world).AsSingle();
            _world.Init();
        }
    }
}