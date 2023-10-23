using System;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Navigation
{
    [Serializable]
    public class PathNode : SampleHolderNode
    {
        private Element _next;
        public override Element Next
        {
            get => _next;
            set => _next = value;
        }
        private Element _previous;
        public override Element Previous
        {
            get => _previous;
            set => _previous = value;
        }

        public IStaticTrajectory Trajectory;

        public override ITrajectoryRefSampler TrajectorySampler => Trajectory;

        protected override void Refresh()
        {
            SampleHolderNode parent = GetParentOfType<SampleHolderNode>();
            ITrajectoryRefSampler toSample = parent.TrajectorySampler;

            toSample.GetOrbitalStateVectorsAtOrbitTime(Time - toSample.Epoch, out DVector3 position, out DVector3 velocity);
            Trajectory.Calculate(position, velocity);
        }
    }
}