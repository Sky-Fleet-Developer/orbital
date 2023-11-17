using Orbital.Core;
using UnityEngine;
using Zenject;

namespace Orbital.Factories
{
    public class StaticBodyFactory : IFactory<GameObject, IStaticBodyAccessor>, IFactory<IStaticBodyAccessor>
    {
        public IStaticBodyAccessor Create()
        {
            return new GameObject("new static body").AddComponent<StaticBody>();
        }

        public IStaticBodyAccessor Create(GameObject owner)
        {
            return owner.AddComponent<StaticBody>();
        }
    }
}
