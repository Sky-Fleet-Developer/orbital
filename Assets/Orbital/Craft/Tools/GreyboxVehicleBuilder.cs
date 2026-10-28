#if UNITY_EDITOR
using System.Collections.Generic;
using Orbital.Core.Config;
using Orbital.Core.Flight;
using UnityEditor;
using UnityEngine;

namespace Orbital.Craft.Tools
{
    /// <summary>
    /// Editor menu that builds the greybox craft prefab from primitives (Assets/_game/Prefabs/Craft/GreyboxVehicle.prefab) with its materials as assets.
    /// The height of the center of mass above the feet comes from CgHeight of Assets/_game/Config/flightconfig.json, so the legs and the flight model agree.
    /// The code is guarded by UNITY_EDITOR because this folder belongs to the Orbital assembly, which is also built into players.
    /// </summary>
    public static class GreyboxVehicleBuilder
    {
        private const string PrefabFolder = "Assets/_game/Prefabs/Craft";
        private const string MaterialFolder = PrefabFolder + "/Materials";
        public const string PrefabPath = PrefabFolder + "/GreyboxVehicle.prefab";
        private const string FlightConfigPath = "Assets/_game/Config/flightconfig.json";

        [MenuItem("Orbital/Build Greybox Vehicle")]
        public static void Build()
        {
            EnsureFolder("Assets/_game/Prefabs", "Craft");
            EnsureFolder(PrefabFolder, "Materials");

            float cgHeight = (float) ReadCgHeight();
            Material template = GreyboxVehicleFactory.DefaultPrimitiveMaterial();
            Dictionary<string, Material> assets = new Dictionary<string, Material>();
            GameObject root = GreyboxVehicleFactory.Build(cgHeight, key => GetMaterialAsset(key, template, assets));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Greybox vehicle saved to {PrefabPath} (center of mass {cgHeight} m above the feet, feet at the origin)", prefab);
            Selection.activeObject = prefab;
        }

        private static Material GetMaterialAsset(string key, Material template, Dictionary<string, Material> cache)
        {
            if (cache.TryGetValue(key, out Material cached)) return cached;
            string path = $"{MaterialFolder}/Greybox_{key}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = GreyboxVehicleFactory.MakeMaterial(template, key);
                AssetDatabase.CreateAsset(material, path);
            }
            cache[key] = material;
            return material;
        }

        private static double ReadCgHeight()
        {
            TextAsset file = AssetDatabase.LoadAssetAtPath<TextAsset>(FlightConfigPath);
            if (file == null)
            {
                Debug.LogWarning($"{FlightConfigPath} not found, the built-in CgHeight is used");
                return FlightConfig.Default().CgHeight;
            }
            return GameConfigLoader.ParseFlight(file.text).CgHeight;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
