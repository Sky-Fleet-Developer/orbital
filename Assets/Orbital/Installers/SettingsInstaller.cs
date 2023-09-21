using Orbital.Model.Data;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class SettingsInstaller : MonoInstaller
    {
        [SerializeField] private OrbitViewSettings orbitViewSettings;
        
        
        public override void InstallBindings()
        {
            Container.Bind().FromInstance(orbitViewSettings).AsSingle();
        }
    }
}