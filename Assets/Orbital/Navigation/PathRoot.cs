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
        [JsonProperty] public DVector3 position;
        [JsonProperty] public DVector3 velocity;

        [JsonIgnore, NonSerialized] public World World;
        [JsonIgnore] private StaticTrajectory _trajectory;
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
        public override IStaticTrajectory Trajectory => _trajectory;
        
        public PathRoot(){}

        public void Init(World world, IStaticBody parent)
        {
            Celestial = parent;
            _trajectory = new StaticTrajectory(parent.MassSystem);
        }

        protected override void Refresh()
        {
            _trajectory.Calculate(position, velocity, Time);
        }
        
        public void AddElement(Element element)
        {
            var last = GetLastElement();
            last.Next = element;
            last.Reconstruct();
            element.SetDirty();
            RefreshDirty();
        }

        public void RefreshDirty()
        {
            GetFirstDirty()?.CallRefresh();
        }
    }
}