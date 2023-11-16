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
                    aSettings.eccentricity = value.eccentricity;
                    aSettings.argumentOfPeriapsis = value.argumentOfPeriapsis;
                    aSettings.longitudeAscendingNode = value.longitudeAscendingNode;
                    aSettings.inclination = value.inclination;
                    aSettings.semiMajorAxis = value.aPericenterRadius;
                    aSettings.epoch = value.timeShift;
                    ChildA.Settings = aSettings;
                }
                {
                    TrajectorySettings bSettings = ChildB.Settings;
                    bSettings.mass = value.bMass;
                    bSettings.eccentricity = value.eccentricity;
                    bSettings.argumentOfPeriapsis = value.argumentOfPeriapsis;
                    bSettings.longitudeAscendingNode = value.longitudeAscendingNode + 180;
                    bSettings.inclination = value.inclination;
                    bSettings.semiMajorAxis = value.aPericenterRadius * Mathf.Pow(value.aMass / value.bMass, 1.0f / 3.0f);
                    bSettings.epoch = value.timeShift;
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
        public bool IsSatellite(IMassSystem subSystem)
        {
            return false;
        }
        [JsonProperty] public IMassSystem ChildA;
        [JsonProperty] public IMassSystem ChildB;
    }
}