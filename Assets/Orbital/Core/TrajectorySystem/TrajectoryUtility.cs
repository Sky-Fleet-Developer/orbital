using Ara3D;

namespace Orbital.Core.TrajectorySystem
{
    public static class TrajectoryUtility
    {
        public static DVector3 GetGlobalPosition(this TreeContainer tree, IMassSystem massSystem, double time)
        {
            DVector3 result = DVector3.Zero;
            IMassSystem cache = massSystem;
            while (cache != tree.Root)
            {
                result += tree._trajectories[cache].GetSample(time, true, false).position;
                tree._parents.TryGetValue(cache, out cache);
            }

            return result;
        }

        public static DVector3 GetGlobalPosition(this TreeContainer tree, IStaticBody staticBody, double time)
        {
            return tree.GetGlobalPosition(tree._massPerComponent[staticBody], time);
        }
        
        
        
        
    }
}