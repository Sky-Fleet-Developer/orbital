using Orbital.Model.Handles;
using UnityEngine;
using Zenject;

namespace Orbital.Model
{
    public class TimeService : MonoBehaviour, IFixedUpdateHandler
    {
        private static double _worldTime;
        
        [Inject]
        private void Inject(DiContainer container)
        {
            HandlerCollection.GetOrCreateCollection<IFixedUpdateHandler>(container).AddItem(this);
        }

        int IOrderHolder.Order => -1;
        void IFixedUpdateHandler.FixedUpdate()
        {
            _worldTime += Time.deltaTime;
        }

        public static double WorldTime => _worldTime;
    }
}
