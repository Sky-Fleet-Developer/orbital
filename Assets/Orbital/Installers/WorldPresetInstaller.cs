using Orbital.Controllers.Data;
using Orbital.Model;
using Orbital.WorldEditor;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class WorldPresetInstaller : MonoInstaller
    {
        [SerializeField] private WorldData preset;
        
        public override void InstallBindings()
        {
            Container.Inject(preset);
            Container.Bind<IFactory<World>>().FromInstance(preset);
        }
    }
}
