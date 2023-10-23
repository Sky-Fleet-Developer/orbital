using Orbital.Core.TrajectorySystem;

namespace Orbital.Navigation
{
    public abstract class SampleHolderNode : Element
    {
        public abstract ITrajectoryRefSampler TrajectorySampler { get; }
    }
}