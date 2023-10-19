using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orbital.Core
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private readonly Queue<Action> _executionQueue = new Queue<Action>();

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock(_executionQueue) {
                while (_executionQueue.Count > 0) {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}