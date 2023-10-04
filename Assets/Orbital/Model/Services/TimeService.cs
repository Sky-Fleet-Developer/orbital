using Orbital.Model.Handles;
using UnityEngine;
using Zenject;

namespace Orbital.Model.Services
{
    public class TimeService : MonoBehaviour, IFixedUpdateHandler
    {
        private double _worldTime;
        
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

        public double WorldTime => _worldTime;
    }
}
