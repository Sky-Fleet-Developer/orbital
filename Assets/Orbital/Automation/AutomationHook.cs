using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Orbital.Automation
{
    /// <summary>
    /// Smoke-run hook for automated player runs (Tools/unity-build.sh smoke). Does nothing unless the player is started with one of:
    /// -orbitalScreenshot &lt;png path&gt; (capture after the delay), -orbitalScreenshotDelay &lt;seconds&gt; (default 10, real time),
    /// -orbitalQuitAfter &lt;seconds&gt; (quit after that many real seconds; with a screenshot the player quits right after it is written).
    /// A test tool only: it is not part of the game and has no effect in a normal launch.
    /// </summary>
    public sealed class AutomationHook : MonoBehaviour
    {
        private string _screenshotPath;
        private float _screenshotDelay = 10f;
        private float _quitAfter = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string screenshot = GetArg("-orbitalScreenshot");
            string quitAfter = GetArg("-orbitalQuitAfter");
            if (screenshot == null && quitAfter == null) return;

            GameObject go = new GameObject("[AutomationHook]");
            DontDestroyOnLoad(go);
            AutomationHook hook = go.AddComponent<AutomationHook>();
            hook._screenshotPath = screenshot;
            if (float.TryParse(GetArg("-orbitalScreenshotDelay"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float delay)) hook._screenshotDelay = delay;
            if (float.TryParse(quitAfter, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float quit)) hook._quitAfter = quit;
            Debug.Log($"[AutomationHook] screenshot '{screenshot}' after {hook._screenshotDelay} s, quit after {hook._quitAfter} s");
        }

        private IEnumerator Start()
        {
            if (_screenshotPath != null)
            {
                yield return new WaitForSecondsRealtime(_screenshotDelay);
                string full = Path.GetFullPath(_screenshotPath);
                string directory = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                if (File.Exists(full)) File.Delete(full);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(full);
                float waited = 0f;
                while (!File.Exists(full) && waited < 10f)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }

                Debug.Log(File.Exists(full) ? $"[AutomationHook] screenshot written: {full}" : $"[AutomationHook] screenshot NOT written: {full}");
                if (_quitAfter < 0f) Application.Quit(0);
            }

            if (_quitAfter >= 0f)
            {
                float elapsed = _screenshotPath != null ? _screenshotDelay : 0f;
                if (_quitAfter > elapsed) yield return new WaitForSecondsRealtime(_quitAfter - elapsed);
                Debug.Log("[AutomationHook] quitting");
                Application.Quit(0);
            }
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
    }
}
