using Orbital.Core.Handles;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public class TimeService : MonoBehaviour, IFixedUpdateHandler
    {
        [ShowInInspector] private static float _timeScale = 1;
        [ShowInInspector] private static double _worldTime;
        
        [Inject]
        private void Inject(DiContainer container)
        {
            HandlerCollection.GetOrCreateCollection<IFixedUpdateHandler>(container).AddItem(this);
        }

        int IOrderHolder.Order => -1;
        void IFixedUpdateHandler.FixedUpdate()
        {
            _worldTime += Time.deltaTime * _timeScale;
        }

        public static double WorldTime => _worldTime;
        public static double TimeScale => _timeScale;
        public static double DeltaTime => Time.deltaTime * _timeScale;
    }
}
