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


        [JsonProperty, ShowInInspector, PropertyOrder(10)] private Element _next;

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

        [JsonIgnore] private StaticOrbit _orbit = new StaticOrbit();
        [JsonIgnore] public override IStaticOrbit Orbit => _orbit;

        protected override void Refresh()
        {
            float scale = 4.456328E-09F;
            
            SampleHolderNode prevSampler = FirstPreviousOfType<SampleHolderNode>();
            
            if (prevSampler.Ending.Type != OrbitEndingType.Cycle && prevSampler.Ending.NextCelestial != null)
            {
                Time = prevSampler.Ending.Time;
                Celestial = prevSampler.Ending.NextCelestial;
            }
            else
            {
                Celestial = prevSampler.Celestial;
            }

            prevSampler.Orbit.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 position, out DVector3 velocity);

            switch (prevSampler.Ending.Type)
            {
                case OrbitEndingType.Leave:
                {
                    prevSampler.Celestial.Orbit.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 pos, out DVector3 vel);
                    position += pos;
                    velocity += vel;
                    Debug.DrawRay(position * scale, velocity * 0.001f, Color.blue, 5);
                }
                    break;
                case OrbitEndingType.Entry:
                {
                    Celestial.Orbit.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 pos, out DVector3 vel);
                    position -= pos;
                    velocity -= vel;
                }
                    break;
            }

            _orbit.Nu = Celestial.GravParameter;
            _orbit.Calculate(position, velocity + _deltaVelocity, Time);
            FindEnding();
        }
    }
}