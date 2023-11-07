using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Navigation
{
    public abstract class PathElement
    {
        //// Navigation
        public abstract PathElement Next { get; set; }
        public abstract PathElement Previous { get; set; }
        public abstract StaticOrbit Orbit { get; }
        public IStaticBody Celestial;
        [ShowInInspector] public OrbitEnding Ending { get; private set; }
        public void FindEnding()
        {
            Ending = Orbit.GetEnding(Celestial, Time);
        }
        
        public PathElement GetLastElement()
        {
            return Next == null ? this : Next.GetLastElement();
        }

        public T FirstPreviousOfType<T>(bool includeThis = false) where T : PathElement
        {
            PathElement e = includeThis ? this : Previous;
            while (e != null)
            {
                if (e is T node) return node;
                e = e.Previous;
            }
            return null;
        }
        public T FirstNextOfType<T>(bool includeThis = false) where T : PathElement
        {
            PathElement e = includeThis ? this : Next;
            while (e != null)
            {
                if (e is T node) return node;
                e = e.Next;
            }
            return null;
        }
        
        public void Reconstruct(List<PathElement> indexList, int myIndex)
        {
            indexList[myIndex] = this;
            if(Next == null) return;
            Next.Previous = this;
            Next.Reconstruct(indexList, myIndex + 1);
        }
        public IEnumerable<PathElement> Enumerate()
        {
            yield return this;
            var element = Next;
            while (element != null)
            {
                yield return element;
                element = element.Next;
            }
        }
        public IEnumerable<T> Enumerate<T>() where  T : PathElement
        {
            foreach (PathElement element in Enumerate())
            {
                if(element is T type) yield return type;
            }
        }
        
        //// Dirty flag
        public bool IsDirty => _isSelfDirty || (Previous?.IsDirty ?? false);
        private bool _isSelfDirty = true;
        public void SetDirty()
        {
            if(IsDirty) return;
            _isSelfDirty = true;
            OnSetDirty();
        }
        public void CallRefresh()
        {
            _isSelfDirty = false;
            Refresh();
            Next?.CallRefresh();
        }

        public PathElement GetFirstDirty()
        {
            if (_isSelfDirty) return this;
            else if(Next != null)
            {
                return Next.GetFirstDirty();
            }
            return null;
        }
        protected abstract void Refresh();
        protected virtual void OnSetDirty(){}

        //// Time position
        private double _time;
        public double Time
        {
            get => _time;
            set
            {
                if (double.IsNaN(value))
                {
                    _time = 0;
                    return;
                }
                _time = Math.Max(Previous?._time ?? 0, value);
                SetDirty();
            }
        }

        // Lifetime
        public void Dispose()
        {
            Previous = null;
            Next?.Dispose();
            Next = null;
            OnDisposed();
        }

        public virtual void OnDisposed(){}
    }
}