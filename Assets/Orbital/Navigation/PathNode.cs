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
        [JsonIgnore, ShowInInspector] private OrbitExodus _exodus;

        [JsonIgnore]
        public override Element Next
        {
            get => _next;
            set => _next = value;
        }

        [JsonIgnore] private Element _previous;

        [JsonIgnore]
        public override Element Previous
        {
            get => _previous;
            set => _previous = value;
        }

        [JsonIgnore] private StaticTrajectory _trajectory = new StaticTrajectory();
        [JsonIgnore] public override IStaticTrajectory Trajectory => _trajectory;

        protected override void Refresh()
        {
            SampleHolderNode prevSampler = GetParentOfType<SampleHolderNode>();
            IStaticTrajectory toSample = prevSampler.Trajectory;
            _exodus = MassUtility.GetOrbitExodus(toSample, prevSampler.Celestial, prevSampler.Time, out _, out double exodusTime, out Celestial);
            if (_exodus != OrbitExodus.Cycle)
            {
                Time = exodusTime;
            }

            toSample.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 position, out DVector3 velocity);

            switch (_exodus)
            {
                case OrbitExodus.Leave:
                {
                    prevSampler.Celestial.Trajectory.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 pos, out DVector3 vel);
                    position += pos;
                    velocity += vel;
                    float scale = 4.456328E-09F;
                    Debug.DrawRay(position * scale, velocity * 0.001f, Color.blue, 5);
                }
                    break;
                case OrbitExodus.Entry:
                {
                    Celestial.Trajectory.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 pos, out DVector3 vel);
                    position -= pos;
                    velocity -= vel;
                }
                    break;
            }

            _trajectory.Nu = Celestial.GravParameter;
            _trajectory.Calculate(position, velocity + _deltaVelocity, Time);
            Debug.Log($"{position} : {_trajectory.GetPositionAtT(Time)}");
        }
    }
}