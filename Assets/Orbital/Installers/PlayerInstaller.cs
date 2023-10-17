using Orbital.Player;
using UnityEngine;
using Zenject;

namespace Orbital.Installers
{
    public class PlayerInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            string id = SystemInfo.deviceUniqueIdentifier;
            Container.BindInstance(new PlayerModel(id)).WithId(PlayerModel.SelfId);
        }
    }
}
