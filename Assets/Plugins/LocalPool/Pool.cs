using System;
using System.Collections.Generic;

namespace Plugins.LocalPool
{
    public class Pool<T>
    {
        public Func<T> MakeNewObject = Activator.CreateInstance<T>;
        public Action<T> OnEnable;
        public Action<T> OnDisable;
        private Queue<T> pool = new Queue<T>();
        
        
        public T Get()
        {
            lock (pool)
            {
                if (pool.TryDequeue(out T result))
                {
                    OnEnable?.Invoke(result);
                    return result;
                }
                else
                {
                    result = MakeNewObject();
                    OnEnable?.Invoke(result);
                    return result;
                }
            }
        }

        public void Put(T value)
        {
            lock (pool)
            {
                pool.Enqueue(value);
                OnDisable?.Invoke(value);
            }
        }
    }
}
