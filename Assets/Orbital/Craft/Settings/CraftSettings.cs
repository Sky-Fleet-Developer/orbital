using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>
    /// The player's craft on this client (Assets/_game/Settings/CraftSettings.asset): its look and the craft model. The physics of a model is in the
    /// shared configs (Assets/_game/Config), not here: the server reads the same numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital/Craft Settings", fileName = "CraftSettings")]
    public class CraftSettings : ScriptableObject
    {
        [Tooltip("Craft model id in craftspec.json; empty = the default model. A start point with its own craftId wins.")]
        public string craftId = "";
        [Tooltip("Optional look of the craft (the prefab from the menu Orbital/Build Greybox Vehicle); empty = the same greybox is built from primitives.")]
        public GameObject vehiclePrefab;
        [Tooltip("Template of the runtime greybox materials (a URP Lit material, e.g. Assets/_game/Prefabs/Craft/Materials/Greybox_hull.mat); required for a player build.")]
        public Material greyboxMaterialTemplate;
        [Tooltip("Hold a landed craft upright until the kernel has a tip-over model (see the stage 2b notes). Switch off to see the inverted pendulum.")]
        public bool groundSupport = true;
    }
}
