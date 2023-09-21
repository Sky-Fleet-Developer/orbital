using Orbital.Controllers.Data;
using Orbital.Controllers.Factories;
using Orbital.Model;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class WorldPresetInstaller : MonoInstaller
    {
        [SerializeField] private WorldPreset preset;
        
        public override void InstallBindings()
        {
            Container.Bind<ISerializer>().FromInstance(new JsonPerformance()).AsSingle();
            Container.Inject(preset);
            Container.Bind<IFactory<World>>().FromInstance(preset);
        }
    }
}
