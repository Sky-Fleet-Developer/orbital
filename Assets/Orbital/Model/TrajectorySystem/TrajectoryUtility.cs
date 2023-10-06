using System;
using Ara3D;
using Orbital.Model.SystemComponents;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public static class TrajectoryUtility
    {
        public static void SetupFromSimulation(this ref TrajectorySettings settings, DVector3 position,
            DVector3 velocity, double parentMass)
        {
            double r = position.Length();

            DVector3 hh = DVector3.Cross(position, velocity);
            DVector3 hhn = hh.Normalize();
            double h = hh.Length();

            //double i = Math.Acos(hh.y / h);

            double vSqr = velocity.LengthSquared();
            // semiMajorAxis
            double a = 1 / (2 / r - vSqr / (MassUtility.G * parentMass));

            double e = Math.Sqrt(1 - (h * h / (MassUtility.G * parentMass * a)));

            /*double omega = Math.Atan2(hh.x, hh.z);
            double cosOmega = Math.Cos(omega);
            double sinOmega = Math.Sin(omega);
            double cosI = Math.Cos(i);
            double sinI = Math.Sin(i);
            double omegaPericenter =
                Math.Atan2(position.z / sinI, (position.x * cosOmega + position.z * sinOmega) / cosI);

            double cosNu = (a * (1 - e * e) / r - 1) / e;
            double sinNu = (position.x * cosOmega + position.y * sinOmega) *
                           (velocity.x * cosOmega + velocity.y * sinOmega) +
                           position.z * velocity.z * r / h / MassUtility.G * parentMass;
            double nu = Math.Atan2(sinNu, cosNu);*/

            double latitudeShift = Math.Asin(hhn.z);
            
            Debug.Log($"Расстояние (r): {r} m");
            Debug.Log($"Большая полуось (a): {a} m");
            Debug.Log($"Эксцентриситет (e): {e}");
            Debug.Log($"latitudeShift : {latitudeShift * RelativeTrajectory.Rad2Deg}");
            //Debug.Log($"Наклонение (i): {i * RelativeTrajectory.Rad2Deg} град");
            //Debug.Log($"Аргумент перицентра (ω): {omegaPericenter * RelativeTrajectory.Rad2Deg} град");
            //Debug.Log($"Долгота восходящего узла (Ω): {omega * RelativeTrajectory.Rad2Deg} град");
            //Debug.Log($"Истинная аномалия (ν): {nu * RelativeTrajectory.Rad2Deg} град");
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

        public static DVector3 GetGlobalPosition(this TreeContainer tree, MassSystemComponent massSystemComponent,
            double time)
        {
            return tree.GetGlobalPosition(tree._componentPerMass[massSystemComponent], time);
        }
    }
}