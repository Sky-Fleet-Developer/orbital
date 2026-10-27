using System;
using System.Collections.Generic;
using System.Linq;

namespace Orbital.Core.Utilities
{
    public class LateEvent
    {
        private List<(Action, int)> actions = new ();
        public bool alredyInvoked { get; private set; }

        public void Subscribe(Action method, int order = 0)
        {
            if (alredyInvoked) method?.Invoke();
            else actions.Add((method, order));
        }

        public void Invoke()
        {
            alredyInvoked = true;
            foreach (var valueTuple in actions.OrderBy(x => -x.Item2))
            {
                valueTuple.Item1?.Invoke();
            }
            actions.Clear();
        }

        public void Reset()
        {
            alredyInvoked = false;
        }
    }
    public class LateEvent<T>
    {
        private List<(Action<T>, int)> actions = new ();
        public bool alredyInvoked { get; private set; }
        public T value;

        public void Subscribe(Action<T> method, int order = 0)
        {
            if (alredyInvoked) method?.Invoke(value);
            else actions.Add((method, order));
        }

        public void Invoke(T argument)
        {
            value = argument;
            alredyInvoked = true;
            foreach (var valueTuple in actions.OrderBy(x => -x.Item2))
            {
                valueTuple.Item1?.Invoke(argument);
            }
            actions.Clear();
        }

        public void Reset()
        {
            alredyInvoked = false;
            actions.Clear();
        }
    }
    
    public class LateEvent<T1, T2>
    {
        public event Action<T1, T2> action;
        public bool alredyInvoked { get; private set; }
        public T1 value1;
        public T2 value2;

        public void Subscribe(Action<T1, T2> method)
        {
            if (alredyInvoked) method?.Invoke(value1, value2);
            else action += method;
        }

        public void Invoke(T1 t1, T2 t2)
        {
            value1 = t1;
            value2 = t2;
            alredyInvoked = true;
            action?.Invoke(t1, t2);
        }

        public void Reset()
        {
            alredyInvoked = false;
        }
    }
}
