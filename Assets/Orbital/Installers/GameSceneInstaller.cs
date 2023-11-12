using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Core.Serialization;
using Orbital.Core.Simulation;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField, FilePath] private string databasePath;
        [SerializeField] private string worldName;
        //[SerializeField] private RigidBodySystemComponent 
        private World _worldInstance;
        public override void InstallBindings()
        {

            _worldInstance = WorldSqlDataAdapter.BuildWorldFromTables(worldName, databasePath);
            Container.Bind<World>().FromInstance(_worldInstance).AsSingle();
            Container.BindInstance(GetComponentInChildren<EventLoopEmitterService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<ComponentsRegistrationService>()).AsSingle();
            Container.BindInstance(GetComponentInChildren<TimeService>()).AsSingle();
        }


        public void OnPostInstall()
        {
            Container.InjectGameObject(_worldInstance.gameObject);
            _worldInstance.Load(false);
        }
    }
}