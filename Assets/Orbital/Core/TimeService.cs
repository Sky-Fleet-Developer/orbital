using Orbital.Core.Handles;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public class TimeService : MonoBehaviour, IFixedUpdateHandler
    {
        [SerializeField] private float timeScale = 1;
        [ShowInInspector] private static double _worldTime;
        
        [Inject]
        private void Inject(DiContainer container)
        {
            HandlerCollection.GetOrCreateCollection<IFixedUpdateHandler>(container).AddItem(this);
        }

        int IOrderHolder.Order => -1;
        void IFixedUpdateHandler.FixedUpdate()
        {
            _worldTime += Time.deltaTime * timeScale;
        }

        public static double WorldTime => _worldTime;
    }
}
