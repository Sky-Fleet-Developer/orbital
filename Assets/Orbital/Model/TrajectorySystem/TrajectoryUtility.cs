using System;
using Ara3D;
using Orbital.Model.SystemComponents;

namespace Orbital.Model.TrajectorySystem
{
    public static class TrajectoryUtility
    {
        public static void SetupFromSimulation(this ref TrajectorySettings settings, DVector3 vector3,
            DVector3 relativePosition)
        {
            throw new NotImplementedException();
        }
        
        public static DVector3 GetGlobalPosition(this TreeContainer tree, IMassSystem massSystem, double time)
        {
            DVector3 result = DVector3.Zero;
            IMassSystem cache = massSystem;
            while (cache != tree.Root)
            {
                result += tree._trajectories[cache].GetPosition(time);
                tree._parents.TryGetValue(cache, out cache);
            }

            return result;
        }

        public static DVector3 GetGlobalPosition(this TreeContainer tree, MassSystemComponent massSystemComponent, double time)
        {
            return tree.GetGlobalPosition(tree._componentPerMass[massSystemComponent], time);
        }

    }
}
