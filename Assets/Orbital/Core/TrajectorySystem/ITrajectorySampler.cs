using Ara3D;

namespace Orbital.Core.TrajectorySystem
{
    public interface ITrajectorySampler
    {
        public (DVector3 position, DVector3 velocity) GetSample(double time);
    }
}
