using System;

namespace Orbital.View.Map
{
    [Serializable]
    public class ScaleSettings
    {
        public float scale;
        [UnityEngine.Tooltip("Exaggeration of all body discs. 1 = real size at the map scale.")]
        public float bodySizeMultiplier = 1f;
    }
}