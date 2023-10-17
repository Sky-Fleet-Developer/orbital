using Orbital.Player;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class PlayerPeripheryInstaller : MonoInstaller
    {
        [SerializeField] private CameraModel playerCamera;

        private CameraModel _camera;
        //[Inject(Id = PlayerModel.SelfId)] private PlayerModel _selfPlayer;
        public override void InstallBindings()
        {
            _camera = Instantiate(playerCamera);
            Container.BindInstance(_camera).AsSingle();
            Container.BindInstance(GetComponentInChildren<CameraControllerService>()).AsSingle();
            

            //Container.Inject(playerCamera);
        }

        public void OnPreResolve()
        {
            Container.Inject(_camera);
        }
    }
}
