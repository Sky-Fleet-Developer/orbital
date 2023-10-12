using System;
using Orbital.Model.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Model.SystemComponents
{
    public class NMRigidbody : SystemComponent<NMRigidbodyVariables, NMRigidbodySettings>
    {
        [SerializeField] private NMRigidbodyVariables variables;
        [SerializeField] private int accuracy;
        [SerializeField] private float nonuniformity;
        public override NMRigidbodyVariables Variables { get => variables; set => variables = value; }
        
[Button]
        private void Simulate()
        {
            IterativeSimulation.DrawTrajectoryCircle(variables.position, variables.velocity, variables.parentMass, accuracy, nonuniformity);     
        }
    }

    [Serializable]
    public struct NMRigidbodyVariables
    {
        public Vector3 velocity;
        public Vector3 position;
        public float parentMass;
    }

    public struct NMRigidbodySettings
    {
        
    }
}
