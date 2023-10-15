using System.Text.RegularExpressions;
using UnityEngine;

namespace Orbital.Core.Utilities
{
    public static class TransformExtension
    {
        public static Transform FindRegex(this Transform transform, string pattern)
        {
            foreach (Transform child in transform)
            {
                if (Regex.IsMatch(child.name, pattern)) return child;
            }

            return null;
        }
    }
}
