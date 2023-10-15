using System;
using System.Collections.Generic;
using Zenject;

namespace Orbital.Core.Handles
{
    public class HandlerCollection
    {
        public static HandlerCollection<T> GetOrCreateCollection<T>(DiContainer container)
        {
            HandlerCollection<T> result = container.TryResolve<HandlerCollection<T>>();
            if (result == null)
            {
                result = new HandlerCollection<T>();
                container.Bind<HandlerCollection<T>>().FromInstance(result);
            }

            return result;
        }
    }
    public class HandlerCollection<T> : HandlerCollection
    {
        public event Action<T> ItemAddedHandler;
        public event Action<T> ItemRemovedHandler;
        private List<T> _list = new List<T>();

        public void AddItem(T item)
        {
            _list.Add(item);
            ItemAddedHandler?.Invoke(item);
        }

        public void RemoveItem(T item)
        {
            int index = _list.IndexOf(item);
            _list[index] = _list[^1];
            _list.RemoveAt(_list.Count - 1);
            ItemRemovedHandler?.Invoke(item);
        }

        public IEnumerable<T> All()
        {
            return _list;
        }
    }
}
