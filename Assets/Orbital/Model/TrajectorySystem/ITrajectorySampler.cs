using Ara3D;

namespace Orbital.Model.TrajectorySystem
{
    public interface ITrajectorySampler
    {
        public (DVector3, DVector3) GetSample(double time);
    }
}
