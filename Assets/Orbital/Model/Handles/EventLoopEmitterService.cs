using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Orbital.Model.Handles
{
    public class EventLoopEmitterService : MonoBehaviour
    {
        public event Action BeforeUpdateHandler;
        public event Action PostUpdateHandler;
        public event Action BeforeFixedUpdateHandler;
        public event Action PostFixedUpdateHandler;
        private HandlerContainer<IUpdateHandler> _updateContainer;
        private HandlerContainer<IFixedUpdateHandler> _fixedUpdateContainer;
        private HandlerContainer<IUpdateByFrequencyHandler> _frequencyUpdateContainer;
        private long _frameCounter;
        [Inject] private DiContainer _diContainer;


        private class HandlerContainer<T> where T : IOrderHolder
        {
            private readonly List<T> _list = new();
            private readonly Comparison<T> _orderComparison = (a, b) => a.Order.CompareTo(b.Order);
            private readonly HandlerCollection<T> _collection;
            public HandlerContainer(DiContainer container)
            {
                _collection = HandlerCollection.GetOrCreateCollection<T>(container);
                _list.AddRange(_collection.All());
                _collection.ItemAddedHandler += OnItemAdded;
                _collection.ItemRemovedHandler += OnItemRemoved;
            }

            private void OnItemRemoved(T v)
            {
                _list.Remove(v);
            }

            private void OnItemAdded(T v)
            {
                _list.Add(v);
                _list.Sort(_orderComparison);
            }

            public IEnumerable<T> All()
            {
                foreach (T orderHolder in _list)
                {
                    yield return orderHolder;
                }
            }

            public void Dispose()
            {
                _collection.ItemAddedHandler -= OnItemAdded;
                _collection.ItemRemovedHandler -= OnItemRemoved;
            }
        }

        private void Start()
        {
            _updateContainer = new HandlerContainer<IUpdateHandler>(_diContainer);
            _fixedUpdateContainer = new HandlerContainer<IFixedUpdateHandler>(_diContainer);
            _frequencyUpdateContainer = new HandlerContainer<IUpdateByFrequencyHandler>(_diContainer);
        }

        private void OnDestroy()
        {
            _updateContainer.Dispose();
            _fixedUpdateContainer.Dispose();
            _frequencyUpdateContainer.Dispose();
        }

        /*private static void RemoveAndShift<T>(List<T> list, T element) where T : class
        {
            int index = list.IndexOf(element);
            list[index] = list[^1];
            list.RemoveAt(list.Count - 1);
        }*/

        private void Update()
        {
            BeforeUpdateHandler?.Invoke();
            foreach (IUpdateHandler handler in _updateContainer.All())
            {
                handler.Update();
            }

            _frameCounter++;
            bool is10Frame = _frameCounter % 10 == 0;
            if (is10Frame)
            {
                UpdateFrequency currentMode = _frameCounter % 1000 == 0 ? UpdateFrequency.Every1000Frame
                    : (_frameCounter % 100 == 0 ? UpdateFrequency.Every100Frame : UpdateFrequency.Every10Frame);
                foreach (IUpdateByFrequencyHandler handler in _frequencyUpdateContainer.All())
                {
                    UpdateFrequency wantedFrequency = handler.Frequency;
                    if (wantedFrequency <= currentMode)
                    {
                        handler.Update();
                    }
                }
            }

            PostUpdateHandler?.Invoke();
        }

        private void FixedUpdate()
        {
            BeforeFixedUpdateHandler?.Invoke();
            foreach (IFixedUpdateHandler handler in _fixedUpdateContainer.All())
            {
                handler.FixedUpdate();
            }
            PostFixedUpdateHandler?.Invoke();
        }
    }
}