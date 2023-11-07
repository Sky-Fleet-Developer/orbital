using System;
using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;

namespace Orbital.Core.Navigation
{
    [Serializable]
    public class NavigationPath : PathElement
    {
        [JsonIgnore] private DVector3 _position;
        [JsonIgnore] private DVector3 _velocity;
        [JsonIgnore] private PathElement _cachedElement;
        [JsonIgnore] private double _cachedTrueAnomaly;
        private double[] _timeIntervals = new double[0];
        private List<PathElement> _allElements = new List<PathElement>();

        [NonSerialized] public World World;
        [JsonIgnore] private StaticOrbit _orbit;
        [JsonProperty, ShowInInspector] private PathNode _next;
        [JsonIgnore] public override PathElement Next
        {
            get => _next;
            set => _next = (PathNode)value;
        }
        [JsonIgnore] public override PathElement Previous
        {
            get => null;
            set { }
        }
        public override StaticOrbit Orbit => _orbit;

        public NavigationPath()
        {
            _allElements.Add(this);
        }

        public void Calculate(IStaticBody parent, DVector3 position, DVector3 velocity, double epoch)
        {
            Celestial = parent;
            _orbit = new StaticOrbit {Nu = parent.GravParameter};
            _position = position;
            _velocity = velocity;
            Time = epoch;
            SetDirty();
            RefreshDirty();
        }

        private void CacheTimeIntervals()
        {
            int count = GetElementsCount() + 1;
            if(_timeIntervals.Length != count) _timeIntervals = new double[count];
            _timeIntervals[0] = -1;
            for (int i = 0; i < _allElements.Count; i++)
            {
                _timeIntervals[i + 1] = _allElements[i].Ending.Time;
            }
        }

        public PathElement GetElementForTime(double time)
        {
            if (time > _timeIntervals[^1])
            {
                this.BuildTransitions(time);
            }
            int left = 1;
            int right = _timeIntervals.Length;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (time >= _timeIntervals[mid - 1] && time <= _timeIntervals[mid])
                {
                    return _allElements[mid - 1];
                }
                else if (time < _timeIntervals[mid])
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            return null;
        }

        public void CacheElementForTime(double time)
        {
            var element = GetElementForTime(time);
            _cachedElement = element;
            _cachedTrueAnomaly = element.Orbit.TrueAnomalyAtT(time);
        }

        public void GetStateFromCacheAtTime(double time, out DVector3 position, out DVector3 velocity)
        {
            _cachedElement.Orbit.GetOrbitalStateVectorsAtTrueAnomaly(_cachedTrueAnomaly, out position, out velocity);
        }
        public DVector3 GetPositionFromCacheAtTime(double time)
        {
            return _cachedElement.Orbit.GetPositionFromTrueAnomaly(_cachedTrueAnomaly);
        }
        public DVector3 GetVelocityFromCacheAtTime(double time)
        {
            return _cachedElement.Orbit.GetOrbitalVelocityAtTrueAnomaly(_cachedTrueAnomaly);
        }
        public IStaticBody GetCachedParent() => _cachedElement.Celestial;

        public int GetElementsCount()
        {
            return _allElements.Count;
        }
        
        public int GetElementsCount<T>()
        {
            PathElement pathElement = this;
            int count = 0;
            while (pathElement != null)
            {
                if(pathElement is T) count++;
                pathElement = pathElement.Next;
            }
            return count;
        }


        protected override void Refresh()
        {
            _orbit.Calculate(_position, _velocity, Time);
            FindEnding();
            RemoveNextIfInfinity();
        }
        
        
        public void AddElement(PathElement pathElement, bool refreshNow = true)
        {
            int count = GetElementsCount();
            var last = GetLastElement();
            last.Next = pathElement;
            _allElements.Add(null);
            Reconstruct(_allElements, count - 1);
            pathElement.SetDirty();
            if (refreshNow)
            {
                RefreshDirty();
            }
        }

        public void RefreshDirty()
        {
            GetFirstDirty()?.CallRefresh();
            CacheTimeIntervals();
        }

        public void RemoveAtElement(PathElement element)
        {
            int index = _allElements.IndexOf(element);
            _allElements.RemoveAt(index);
            element.Dispose();
            _allElements[index - 1].Next = null;
        }

        public StaticOrbit GetOrbitAtTime(double time)
        {
            return GetElementForTime(time).Orbit;
        }

        public IStaticBody GetParentAtTime(double time)
        {
            return GetElementForTime(time).Celestial;
        }

        public override void OnDisposed()
        {
            _allElements.Clear();
        }
    }
}