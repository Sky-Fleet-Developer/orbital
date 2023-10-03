using System;
using System.Collections.Generic;
using Ara3D;
using Newtonsoft.Json;
using Orbital.Model.SystemComponents;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    [Serializable]
    public class DoubleSystemBranch : IMass
    {
        public double Mass => (ChildA?.Mass ?? 0) + (ChildB?.Mass ?? 0);
        [SerializeField, JsonProperty] private TrajectorySettings settings;
        [SerializeField, JsonProperty] private DoubleSystemTrajectorySettings localTrajectorySettings;
        public TrajectorySettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public DoubleSystemTrajectorySettings LocalTrajectorySettings
        {
            get => localTrajectorySettings;
            set
            {
                localTrajectorySettings = value;
                {
                    TrajectorySettings aSettings = ChildA.Settings;
                    aSettings.mass = value.aMass;
                    aSettings.period = value.period;
                    aSettings.latitudeShift = value.latitudeShift;
                    aSettings.longitudeShift = value.longitudeShift;
                    aSettings.inclination = value.inclination;
                    aSettings.pericenterRadius = value.aPericenterRadius;
                    aSettings.timeShift = value.timeShift;
                    ChildA.Settings = aSettings;
                }
                {
                    TrajectorySettings bSettings = ChildB.Settings;
                    bSettings.mass = value.bMass;
                    bSettings.period = value.period;
                    bSettings.latitudeShift = -value.latitudeShift;
                    bSettings.longitudeShift = value.longitudeShift + 180;
                    bSettings.inclination = -value.inclination;
                    bSettings.pericenterRadius = value.aPericenterRadius * Mathf.Pow(value.aMass / value.bMass, 1.0f / 3.0f);
                    bSettings.timeShift = value.timeShift;
                    ChildB.Settings = bSettings;
                }
            } 
        }
        public DoubleSystemBranch()
        {
            ChildA = null;
            ChildB = null;
        }
        public DoubleSystemBranch(IMass childA, IMass childB)
        {
            ChildA = childA;
            ChildB = childB;
        }

        public IEnumerable<IMass> GetContent()
        {
            yield return ChildA;
            yield return ChildB;
        }

        [JsonProperty] public IMass ChildA;
        [JsonProperty] public IMass ChildB;
    }
}