using System;

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
        public T GetParentOfType<T>() where T : class
        {
            Element e = Previous;
            while (e != null)
            {
                if (e is T node) return node;
                e = e.Previous;
            }
            return null;
        }
        
        //// Dirty flag
        public bool IsDirty => _isSelfDirty || (Previous?.IsDirty ?? false);
        private bool _isSelfDirty;
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
        protected abstract void Refresh();
        protected virtual void OnSetDirty(){}

        //// Time position
        private double _time;
        public double Time
        {
            get => _time;
            set
            {
                _time = Math.Max(Previous._time, value);
                if (Next != null)
                {
                    _time = Math.Min(Next._time, _time);
                }
                SetDirty();
            }
        }
    }
}