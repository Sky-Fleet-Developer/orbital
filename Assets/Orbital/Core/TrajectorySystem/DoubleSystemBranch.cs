using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
{
    [Serializable]
    public class DoubleSystemBranch : IMassSystem
    {
        public double Mass => (ChildA?.Mass ?? 0) + (ChildB?.Mass ?? 0);
        [SerializeField, JsonProperty] private TrajectorySettings settings;
        [SerializeField, JsonProperty] private DoubleSystemTrajectorySettings localTrajectorySettings;
        public TrajectorySettings Settings
        {
            get => settings;
            set => settings = value;
        }

        [JsonIgnore] public DoubleSystemTrajectorySettings LocalTrajectorySettings
        {
            get => localTrajectorySettings;
            set
            {
                localTrajectorySettings = value;
                {
                    TrajectorySettings aSettings = ChildA.Settings;
                    aSettings.mass = value.aMass;
                    aSettings.period = value.period;
                    aSettings.argumentOfPeriapsis = value.latitudeShift;
                    aSettings.longitudeAscendingNode = value.longitudeShift;
                    aSettings.inclination = value.inclination;
                    aSettings.semiMajorAxis = value.aPericenterRadius;
                    aSettings.timeShift = value.timeShift;
                    ChildA.Settings = aSettings;
                }
                {
                    TrajectorySettings bSettings = ChildB.Settings;
                    bSettings.mass = value.bMass;
                    bSettings.period = value.period;
                    bSettings.argumentOfPeriapsis = -value.latitudeShift;
                    bSettings.longitudeAscendingNode = value.longitudeShift + 180;
                    bSettings.inclination = -value.inclination;
                    bSettings.semiMajorAxis = value.aPericenterRadius * Mathf.Pow(value.aMass / value.bMass, 1.0f / 3.0f);
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
        public DoubleSystemBranch(IMassSystem childA, IMassSystem childB)
        {
            ChildA = childA;
            ChildB = childB;
        }

        public IEnumerable<IMassSystem> GetContent()
        {
            yield return ChildA;
            yield return ChildB;
        }

        [JsonProperty] public IMassSystem ChildA;
        [JsonProperty] public IMassSystem ChildB;
    }
}