using System.Collections.Generic;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public static class MassUtility
    {
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
                foreach (IMass mass in content.GetRecursively())
                {
                    yield return mass;
                }
            }
        }
    }
}