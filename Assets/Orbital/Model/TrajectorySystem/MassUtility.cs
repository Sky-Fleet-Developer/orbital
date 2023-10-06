using System.Collections.Generic;
using Orbital.Model.Utilities;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public static class MassUtility
    {
        public const double G = 6.67430e-11;
        
        public static Dictionary<IMassSystem, Transform> GetMap(this IMassSystem mRoot, Transform tRoot)
        {
            Dictionary<IMassSystem, Transform> result = new();
            result.Add(mRoot, tRoot);
            AddMapRecursively(result, mRoot, tRoot);
            return result;
        }

        private static void AddMapRecursively(Dictionary<IMassSystem, Transform> dictionary, IMassSystem mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMassSystem mass in mRoot.GetContent())
            {
                i++;
                if (mass == null) continue;
                Transform tChild = tRoot.FindRegex($".*\\[{i}\\]$");
                if (tChild != null)
                {
                    dictionary.Add(mass, tChild);
                    AddMapRecursively(dictionary, mass, tChild);
                }
            }
        }

        public static IEnumerable<IMassSystem> GetRecursively(this IMassSystem value)
        {
            foreach (IMassSystem content in value.GetContent())
            {
                yield return content;
                if (content == null) continue;
                foreach (IMassSystem mass in content.GetRecursively())
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

        public static void FillTrajectoriesRecursively(this IMassSystem massSystem, Dictionary<IMassSystem, RelativeTrajectory> container)
        {
            void SetupElement(IMassSystem child, IMassSystem other, SystemType type)
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

            if (massSystem is DoubleSystemBranch doubleSystemBranch)
            {
                SetupElement(doubleSystemBranch.ChildA, doubleSystemBranch.ChildB, SystemType.DoubleSystem);
                SetupElement(doubleSystemBranch.ChildB, doubleSystemBranch.ChildA, SystemType.DoubleSystem);
            }
            else
            {
                foreach (IMassSystem child in massSystem.GetContent())
                {
                    SetupElement(child, massSystem, SystemType.SingleCenter);
                }
            }
        }
    }
}