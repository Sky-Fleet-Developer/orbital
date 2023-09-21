using System;
using System.Collections.Generic;
using Orbital.Model.Handles;
using UnityEngine;

namespace Orbital.Model.Services
{
    public class LoopEmitterService : MonoBehaviour
    {
        public event Action BeforeUpdateHandler;
        public event Action PostUpdateHandler;
        public event Action BeforeFixedUpdateHandler;
        public event Action PostFixedUpdateHandler;
        private readonly List<IUpdateHandler> _updateHandlers = new();
        private readonly List<IFixedUpdateHandler> _fixedUpdateHandlers = new();

        public void Add<T>(T value) where T : class
        {
            if (value is IUpdateHandler updateHandler)
            {
                _updateHandlers.Add(updateHandler);
            }
            if (value is IFixedUpdateHandler fixedUpdateHandler)
            {
                _fixedUpdateHandlers.Add(fixedUpdateHandler);
            }
        }

        public void Remove<T>(T value) where T : class
        {
            if (value is IUpdateHandler updateHandler)
            {
                RemoveAndShift(_updateHandlers, updateHandler);
            }
            if (value is IFixedUpdateHandler fixedUpdateHandler)
            {
                RemoveAndShift(_fixedUpdateHandlers, fixedUpdateHandler);
            }
        }

        private static void RemoveAndShift<T>(List<T> list, T element) where T : class
        {
            int index = list.IndexOf(element);
            list[index] = list[^1];
            list.RemoveAt(list.Count - 1);
        }

        private void Update()
        {
            BeforeUpdateHandler?.Invoke();
            for (int i = 0; i < _updateHandlers.Count; i++)
            {
                _updateHandlers[i].Update();
            }
            PostUpdateHandler?.Invoke();
        }

        private void FixedUpdate()
        {
            BeforeFixedUpdateHandler?.Invoke();
            for (int i = 0; i < _fixedUpdateHandlers.Count; i++)
            {
                _fixedUpdateHandlers[i].FixedUpdate();
            }
            PostFixedUpdateHandler?.Invoke();
        }
    }
}
