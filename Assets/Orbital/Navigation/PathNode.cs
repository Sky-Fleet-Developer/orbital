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
    public class PathNode : SampleHolderNode
    {
        [JsonProperty, ShowInInspector] private DVector3 _deltaVelocity;
        
        
        [JsonProperty, ShowInInspector] private Element _next;
        [JsonIgnore] public override Element Next
        {
            get => _next;
            set => _next = value;
        }
        [JsonIgnore] private Element _previous;
        [JsonIgnore] public override Element Previous
        {
            get => _previous;
            set => _previous = value;
        }

        [JsonIgnore] public StaticTrajectory Trajectory = new StaticTrajectory();
        [JsonIgnore] public override ITrajectoryRefSampler TrajectorySampler => Trajectory;

        protected override void Refresh()
        {
            SampleHolderNode prevSampler = GetParentOfType<SampleHolderNode>();
            ITrajectoryRefSampler toSample = prevSampler.TrajectorySampler;
            toSample.GetOrbitalStateVectorsAtOrbitTime(Time - toSample.Epoch, out DVector3 position, out DVector3 velocity);
            IStaticBody prevNodeCelestial = prevSampler.Celestial;

            double radius = MassUtility.GetGravityRadius(prevNodeCelestial.GravParameter);
            if (position.Length() > radius)
            {
                Celestial = prevNodeCelestial.Parent;
                position += Celestial.Trajectory.GetPositionAtT(Time - toSample.Epoch);
            }
            else
            {
                
            }
            
            Trajectory.Calculate(position, velocity + _deltaVelocity);
            
            
        }
    }
}