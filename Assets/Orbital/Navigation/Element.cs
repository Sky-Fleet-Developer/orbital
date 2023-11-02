using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Navigation
{
    public abstract class Element
    {
        //// Navigation
        public abstract Element Next { get; set; }
        public abstract Element Previous { get; set; }
        public Element GetLastElement()
        {
            return Next == null ? this : Next.GetLastElement();
        }

        public T FirstPreviousOfType<T>(bool includeThis = false) where T : Element
        {
            Element e = includeThis ? this : Previous;
            while (e != null)
            {
                if (e is T node) return node;
                e = e.Previous;
            }
            return null;
        }
        public T FirstNextOfType<T>(bool includeThis = false) where T : Element
        {
            Element e = includeThis ? this : Next;
            while (e != null)
            {
                if (e is T node) return node;
                e = e.Next;
            }
            return null;
        }
        
        public void Reconstruct()
        {
            if(Next == null) return;
            Next.Previous = this;
            Next.Reconstruct();
        }
        public IEnumerable<Element> Enumerate()
        {
            yield return this;
            var element = Next;
            while (element != null)
            {
                yield return element;
                element = element.Next;
            }
        }
        //// Dirty flag
        [JsonIgnore] public bool IsDirty => _isSelfDirty || (Previous?.IsDirty ?? false);
        [JsonProperty] private bool _isSelfDirty = true;
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

        public Element GetFirstDirty()
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
        [JsonProperty] private double _time;
        [JsonIgnore, ShowInInspector] public double Time
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
    }
}