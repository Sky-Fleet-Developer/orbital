using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Core.Serialization;
using Orbital.Core.Serialization.Sqlite;
using Orbital.Core.Simulation;
using Orbital.Factories;
using Orbital.Serialization.SqlModel;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class GameSceneInstaller : MonoInstaller
    {
        [SerializeField, FilePath] private string databasePath;
        [SerializeField] private Declaration sqliteDeclaration;
        //[SerializeField] private RigidBodySystemComponent 
        private World _worldInstance;
        private WorldSet _worldSet;
        private WorldContext _worldContext;
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<StaticBodyFactory>().FromInstance(new StaticBodyFactory());
            _worldSet = new WorldSet(sqliteDeclaration, $"Data Source={databasePath}");
            Container.Inject(_worldSet);
            _worldContext = _worldSet.LoadWorld();
            _worldInstance = _worldContext.World;
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