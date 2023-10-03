using System.Collections.Generic;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public static class MassUtility
    {
        public const double G = 6.67430e-11;
        
        public static Dictionary<IMass, Transform> GetMap(this IMass mRoot, Transform tRoot)
        {
            Dictionary<IMass, Transform> result = new();
            result.Add(mRoot, tRoot);
            AddMapRecursively(result, mRoot, tRoot);
            return result;
        }

        private static void AddMapRecursively(Dictionary<IMass, Transform> dictionary, IMass mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMass mass in mRoot.GetContent())
            {
                i++;
                if (mass == null) continue;
                string wantedName = $"Child[{i}]";
                Transform tChild = tRoot.Find(wantedName);
                if (tChild != null)
                {
                    dictionary.Add(mass, tChild);
                    AddMapRecursively(dictionary, mass, tChild);
                }
            }
        }

        public static IEnumerable<IMass> GetRecursively(this IMass value)
        {
            foreach (IMass content in value.GetContent())
            {
                yield return content;
                if (content == null) continue;
                foreach (IMass mass in content.GetRecursively())
                {
                    yield return mass;
                }
            }
        }

        /*public static Dictionary<IMass, RelativeTrajectory> MakeTrajectories(this IMass mRoot)
        {
            Dictionary<IMass, RelativeTrajectory> result = new();
            FillTrajectoriesRecursively(result, mRoot);
            return result;
        }*/

        public static void FillTrajectoriesRecursively(this IMass mass, Dictionary<IMass, RelativeTrajectory> container)
        {
            void SetupElement(IMass child, IMass other, SystemType type)
            {
                if (child == null) return;
                if (other == null) return;
                if (!container.TryGetValue(child, out RelativeTrajectory trajectory))
                {
                    trajectory = new RelativeTrajectory(child, other, type);
                    container.Add(child, trajectory);
                }

                trajectory.Calculate();
                FillTrajectoriesRecursively(child, container);
            }

            if (mass is DoubleSystemBranch doubleSystemBranch)
            {
                SetupElement(doubleSystemBranch.ChildA, doubleSystemBranch.ChildB, SystemType.DoubleSystem);
                SetupElement(doubleSystemBranch.ChildB, doubleSystemBranch.ChildA, SystemType.DoubleSystem);
            }
            else
            {
                foreach (IMass child in mass.GetContent())
                {
                    SetupElement(child, mass, SystemType.SingleCenter);
                }
            }
        }
    }
}