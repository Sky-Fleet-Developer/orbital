using System;
using Orbital.Model.TrajectorySystem;
using UnityEngine;

namespace Orbital.Model.SystemComponents
{
    public class RigidBodySystemComponent : SystemComponent<RigidBodyVariables, RigidBodySettings>
    {
        
    }
    
    [Serializable]
    public struct RigidBodySettings
    {
        public Rigidbody dynamicPresentation;
    }

    [Serializable]
    public struct RigidBodyVariables
    {
        public TrajectorySettings trajectorySettings;
        
    }

    [Serializable]
    public struct SimulationVariables
    {
        public Vector3 velocity;
        public Vector3 position;
    }
}
