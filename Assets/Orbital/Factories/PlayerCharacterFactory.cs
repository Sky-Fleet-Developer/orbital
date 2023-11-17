using Orbital.Core;
using UnityEngine;
using Zenject;

namespace Orbital.Factories
{
    public class PlayerCharacterFactory : IFactory<PlayerCharacter>
    {
        public PlayerCharacter Create()
        {
            GameObject instance = new GameObject("Player");
            PlayerCharacter component = instance.AddComponent<PlayerCharacter>();
            return component;
        }
    }
}