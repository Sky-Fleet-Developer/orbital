using Ara3D;

namespace Orbital.Model.TrajectorySystem
{
    public interface ITrajectorySampler
    {
        public (DVector3 position, DVector3 velocity) GetSample(double time);
    }
}
