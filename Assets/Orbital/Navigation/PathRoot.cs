using System;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Navigation
{
    [Serializable]
    public class PathRoot : SampleHolderNode
    {
        [JsonProperty] public float rotation;
        [JsonProperty] public Vector3 position;
        [JsonProperty] public Vector3 velocity;

        [JsonIgnore, NonSerialized] public World World;
        [JsonIgnore] private StaticOrbit _orbit;
        [JsonProperty, ShowInInspector] private PathNode _next;
        [JsonIgnore] public override Element Next
        {
            get => _next;
            set => _next = (PathNode)value;
        }
        [JsonIgnore] public override Element Previous
        {
            get => null;
            set { }
        }
        public override IStaticOrbit Orbit => _orbit;
        
        public PathRoot(){}

        public void Init(World world, IStaticBody parent)
        {
            Celestial = parent;
            _orbit = new StaticOrbit(parent.MassSystem);
        }

        public int GetElementsCount()
        {
            Element element = this;
            int count = 0;
            while (element != null)
            {
                count++;
                element = element.Next;
            }
            return count;
        }

        protected override void Refresh()
        {
            Vector3 h = Vector3.Cross(position, velocity).normalized;
            Quaternion quaternion = Quaternion.AngleAxis(rotation, h);
            _orbit.Calculate(quaternion * position, quaternion * velocity, Time);
            Debug.Log($"v : {quaternion * velocity}, p': {_orbit.GetPositionAtT(Time)}, v': {_orbit.GetOrbitalVelocityAtOrbitTime(Time)}");
            FindEnding();
        }
        
        public void AddElement(Element element, bool refreshNow = true)
        {
            var last = GetLastElement();
            last.Next = element;
            last.Reconstruct();
            element.SetDirty();
            if(refreshNow) RefreshDirty();
        }

        public void RefreshDirty()
        {
            GetFirstDirty()?.CallRefresh();
        }
    }
}