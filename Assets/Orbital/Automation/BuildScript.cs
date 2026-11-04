#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Orbital.Core;
using Orbital.Core.Config;
using Orbital.Core.Worlds;
using Orbital.Craft;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orbital.Automation
{
    /// <summary>
    /// Batch-mode entry points for Tools/unity-build.sh (Unity CLI: unity run ... -- -executeMethod Orbital.Automation.BuildScript.X).
    /// Guarded by UNITY_EDITOR because this folder belongs to the Orbital assembly, which is also built into players.
    /// Arguments: -buildOutput &lt;path to the exe&gt; (default Build/Windows/Orbital.exe), -development (development build, enables the HUD time slider).
    /// The editor exits with 0 on success and 1 on failure, so the shell script can rely on the exit code.
    /// </summary>
    public static class BuildScript
    {
        private const string DefaultOutput = "Build/Windows/Orbital.exe";

        /// <summary>CraftSpawner object fields that must be set for a player build (no AssetDatabase fallback there) and what they should hold.</summary>
        private static readonly (string field, string file)[] CraftConfigFields =
        {
            ("flightConfigJson", GameConfigLoader.FlightFile),
            ("fuelsJson", GameConfigLoader.FuelsFile),
            ("craftSpecJson", GameConfigLoader.CraftSpecFile),
            ("startPointsJson", GameConfigLoader.StartPointsFile),
            ("networkSettings", "Assets/_game/Settings/NetworkSettings.asset"),
            ("craftSettings", "Assets/_game/Settings/CraftSettings.asset"),
            ("viewSettings", "the container Assets/_game/ViewSettings/ViewSettingsContainer.asset"),
        };

        public static void BuildWindows()
        {
            string output = GetArg("-buildOutput") ?? DefaultOutput;
            bool development = HasArg("-development");
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildScript] no enabled scenes in Build Settings");
                EditorApplication.Exit(1);
                return;
            }

            if (!ValidateCraftConfigs(scenes) | !ValidateWorlds(scenes))
            {
                EditorApplication.Exit(1);
                return;
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            Debug.Log($"[BuildScript] building {string.Join(", ", scenes)} -> {output} (development: {development})");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[BuildScript] result {summary.result}, errors {summary.totalErrors}, warnings {summary.totalWarnings}, size {summary.totalSize} bytes, time {summary.totalTime}");
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Opens the project, lets it compile and import, then exits with 0. Compile errors make batch mode exit non-zero by itself.</summary>
        public static void CompileCheck()
        {
            Debug.Log("[BuildScript] compile check passed");
            EditorApplication.Exit(0);
        }

        private static bool ValidateCraftConfigs(string[] scenes)
        {
            bool ok = true;
            foreach (string path in scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (CraftSpawner spawner in FindSpawners(scene))
                {
                    SerializedObject so = new SerializedObject(spawner);
                    foreach ((string field, string expected) in CraftConfigFields)
                    {
                        SerializedProperty property = so.FindProperty(field);
                        if (property != null && property.objectReferenceValue != null) continue;
                        Debug.LogError($"[BuildScript] {path}: {spawner.name}.{field} is empty (expected: {expected}), the player would miss it (assign it in the Inspector; do not save Main.unity from batch mode: loading the world rewrites orbit epochs in the scene)");
                        ok = false;
                    }
                    if (so.FindProperty("craftSettings")?.objectReferenceValue is CraftSettings craft && craft.greyboxMaterialTemplate == null)
                    {
                        Debug.LogError($"[BuildScript] {path}: {spawner.name}.craftSettings has no greyboxMaterialTemplate, the player would draw magenta (expected a URP material, e.g. Assets/_game/Prefabs/Craft/Materials/Greybox_hull.mat)");
                        ok = false;
                    }
                    ok &= ValidateViewSettings(path, spawner, so.FindProperty("viewSettings")?.objectReferenceValue as ScriptableObjectContainer.Container);
                }
            }

            return ok;
        }

        // Every section of the flight view settings must be in the container once (a missing one would silently run on the default values).
        private static bool ValidateViewSettings(string path, CraftSpawner spawner, ScriptableObjectContainer.Container container)
        {
            if (container == null) return true; // reported as an empty field above
            bool ok = true;
            ScriptableObject[] assets = container.GetAssetsAtType<ScriptableObject>().ToArray();
            foreach (System.Reflection.PropertyInfo section in typeof(Orbital.View.Flight.FlightViewSettings).GetProperties())
            {
                int count = assets.Count(asset => asset != null && asset.GetType() == section.PropertyType);
                if (count == 1) continue;
                Debug.LogError($"[BuildScript] {path}: {spawner.name}.viewSettings has {count} of {section.PropertyType.Name}, expected 1 (menu Orbital/Add Missing Flight View Settings)");
                ok = false;
            }
            return ok;
        }

        /// <summary>
        /// Stage 2c.W: a World that reads the shared world file must have the TextAsset set (the player has no AssetDatabase fallback and would
        /// fall back to the scene tree with an error). A World with the source Scene (offline tests) is reported but allowed.
        /// </summary>
        private static bool ValidateWorlds(string[] scenes)
        {
            bool ok = true;
            foreach (string path in scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (World world in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<World>(true)))
                {
                    SerializedObject so = new SerializedObject(world);
                    SerializedProperty source = so.FindProperty("source");
                    if (source != null && source.enumValueIndex == (int) WorldSource.Scene)
                    {
                        Debug.LogWarning($"[BuildScript] {path}: {world.name} takes the world from the scene tree (source Scene), not from the shared {WorldFileHash.FileName}");
                        continue;
                    }

                    SerializedProperty file = so.FindProperty("worldFile");
                    if (file != null && file.objectReferenceValue != null) continue;
                    Debug.LogError($"[BuildScript] {path}: {world.name}.worldFile is empty (expected: Assets/_game/Config/{WorldFileHash.FileName}), the player would use the scene tree " +
                                   "(assign it in the Inspector or add one line to the World component in the scene YAML; do not save Main.unity from batch mode)");
                    ok = false;
                }
            }

            return ok;
        }

        private static CraftSpawner[] FindSpawners(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CraftSpawner>(true)).ToArray();
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }

            return null;
        }

        private static bool HasArg(string name)
        {
            return Environment.GetCommandLineArgs().Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
#endif
