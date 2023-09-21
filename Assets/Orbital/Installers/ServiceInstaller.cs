using Orbital.Model.Services;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class ServiceInstaller : MonoInstaller
    {
        [SerializeField] private LoopEmitterService loopEmitterService;
        [SerializeField] private ComponentsRegistrationService componentsRegistrationService;
        
        public override void InstallBindings()
        {
            Container.BindInstance(componentsRegistrationService).AsSingle();
            Container.BindInstance(loopEmitterService).AsSingle();
        }
    }
}