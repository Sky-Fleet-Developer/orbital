#if UNITY_EDITOR
using System;
using System.Linq;
using Orbital.View.Flight;
using ScriptableObjectContainer;
using UnityEditor;
using UnityEngine;

namespace Orbital.Craft.Tools
{
    /// <summary>
    /// Adds the sections of the flight view settings that the view settings container does not have yet, with their default values.
    /// Idempotent: an existing section keeps its values. Guarded by UNITY_EDITOR because this folder belongs to the Orbital assembly.
    /// </summary>
    public static class FlightViewSettingsTool
    {
        public const string ContainerPath = "Assets/_game/ViewSettings/ViewSettingsContainer.asset";

        // Every section is a property of FlightViewSettings, so a new topic needs no change here.
        private static readonly Type[] Sections = typeof(FlightViewSettings).GetProperties().Select(property => property.PropertyType).ToArray();

        [MenuItem("Orbital/Add Missing Flight View Settings")]
        public static void AddMissing()
        {
            Container container = AssetDatabase.LoadAssetAtPath<Container>(ContainerPath);
            if (container == null)
            {
                Debug.LogError($"FlightViewSettingsTool: no container at {ContainerPath}");
                return;
            }
            var present = container.GetAssetsAtType<ScriptableObject>().Select(asset => asset.GetType()).ToArray();
            foreach (Type type in Sections.Where(type => !present.Contains(type)))
            {
                ScriptableObject section = ScriptableObject.CreateInstance(type);
                section.name = type.Name;
                container.AddNestedAsset(section);
                Debug.Log($"FlightViewSettingsTool: added {type.Name}");
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Entry point for batch mode (-executeMethod).</summary>
        public static void AddMissingAndExit()
        {
            AddMissing();
            EditorApplication.Exit(0);
        }
    }
}
#endif
