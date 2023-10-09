using System;
using System.Threading.Tasks;
using Ara3D;
using Ara3D.Double;
using Orbital.Model.SystemComponents;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public static class TrajectoryUtility
    {
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

        public static DVector3 GetGlobalPosition(this TreeContainer tree, MassSystemComponent massSystemComponent,
            double time)
        {
            return tree.GetGlobalPosition(tree._componentPerMass[massSystemComponent], time);
        }
    }
}