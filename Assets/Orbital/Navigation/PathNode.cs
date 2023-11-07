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
    public class PathNode : PathElement
    {
        [JsonProperty, ShowInInspector] private DVector3 _deltaVelocity;


        [JsonProperty, ShowInInspector, PropertyOrder(10)] private PathElement _next;

        [JsonIgnore]
        public override PathElement Next
        {
            get => _next;
            set => _next = value;
        }

        [JsonIgnore] private PathElement _previous;

        [JsonIgnore]
        public override PathElement Previous
        {
            get => _previous;
            set => _previous = value;
        }

        [JsonIgnore] private StaticOrbit _orbit = new StaticOrbit();
        [JsonIgnore] public override StaticOrbit Orbit => _orbit;

        protected override void Refresh()
        {
            float scale = 4.456328E-09F;
            
            if (Previous.Ending.Type != OrbitEndingType.Cycle && Previous.Ending.NextCelestial != null)
            {
                Time = Previous.Ending.Time;
                Celestial = Previous.Ending.NextCelestial;
            }
            else
            {
                Celestial = Previous.Celestial;
            }

            Previous.Orbit.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 position, out DVector3 velocity);

            switch (Previous.Ending.Type)
            {
                case OrbitEndingType.Leave:
                {
                    Previous.Celestial.Orbit.GetOrbitalStateVectorsAtOrbitTime(Time, out DVector3 pos, out DVector3 vel);
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