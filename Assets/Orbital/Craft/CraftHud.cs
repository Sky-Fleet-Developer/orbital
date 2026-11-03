using Orbital.Core;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>
    /// Greybox HUD: a text panel and four bars, drawn with IMGUI. The project has no UI of its own yet, and IMGUI needs no assets, no canvas and no
    /// scene change (the spawner creates this component), so it is the simplest thing that works; the cockpit replaces it later.
    /// The spawner fills the public fields a few times per second.
    /// </summary>
    public sealed class CraftHud : MonoBehaviour
    {
        public string Telemetry = string.Empty;
        public string Banner = string.Empty;
        public string Help = string.Empty;
        public bool ShowHelp = true;
        public float Throttle;
        public float MainFuel;
        public float RcsFuel;
        public float Gyro;
        public bool RcsOn;
        /// <summary>The navigation ball (stage 2b.1); drawn at the bottom center when it has data. Set by the spawner.</summary>
        public NavBallView NavBall;
        /// <summary>One line under the ball: nose angles, speed mode.</summary>
        public string NavCaption = string.Empty;
        /// <summary>Show the time scale slider (stage 2b.2, a test tool of the offline flight; the spawner shows it only in the Editor and development builds).</summary>
        public bool ShowTimeScale;
        /// <summary>One line next to the slider: set scale, measured real rate, the cap.</summary>
        public string TimeScaleInfo = string.Empty;
        /// <summary>
        /// Online (stage 2c.6): when set, the slider does not change the local time but sends this request (admin sessions only; the spawner
        /// sets it). The value goes out after the slider has been still for <see cref="RequestDelay"/> s; the shown scale is the server's.
        /// </summary>
        public System.Action<double> RequestTimeScale;
        public const float RequestDelay = 0.3f;
        /// <summary>Online: one network status line above the telemetry panel (no secrets).</summary>
        public string NetLine = string.Empty;
        /// <summary>Online (stage 2c.7): other crafts, one per line (callsign, distance, mode); drawn bottom left.</summary>
        public string Others = string.Empty;
        private double _pendingScale = double.NaN;
        private float _pendingAt;

        private void Update()
        {
            if (double.IsNaN(_pendingScale) || RequestTimeScale == null) return;
            if (Time.unscaledTime - _pendingAt < RequestDelay) return;
            double scale = _pendingScale;
            _pendingScale = double.NaN;
            RequestTimeScale(scale);
        }

        private GUIStyle _label;
        private GUIStyle _banner;
        private GUIStyle _letters;
        private GUIStyle _caption;

        private void OnGUI()
        {
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) {fontSize = 14, wordWrap = false};
                _label.normal.textColor = Color.white;
                _banner = new GUIStyle(GUI.skin.label) {fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter};
                _banner.normal.textColor = new Color(1f, 0.85f, 0.2f);
                _letters = new GUIStyle(GUI.skin.label) {fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false};
                _letters.normal.textColor = Color.white;
                _caption = new GUIStyle(GUI.skin.label) {fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = false};
                _caption.normal.textColor = Color.white;
            }
            Rect panel = new Rect(10, 10, 400, 380);
            if (!string.IsNullOrEmpty(NetLine))
            {
                GUI.Box(new Rect(panel.x, panel.y, 760, 24), GUIContent.none);
                GUI.Label(new Rect(panel.x + 8, panel.y + 2, 744, 22), NetLine, _label);
                panel.y += 28;
            }
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 8, panel.y + 6, panel.width - 16, 282), Telemetry, _label);
            float y = panel.y + 292;
            Bar(ref y, "THR", Throttle, new Color(1f, 0.6f, 0.15f));
            Bar(ref y, "FUEL", MainFuel, new Color(0.3f, 0.8f, 0.35f));
            Bar(ref y, RcsOn ? "RCS on" : "RCS off", RcsFuel, RcsOn ? new Color(0.35f, 0.75f, 1f) : new Color(0.45f, 0.5f, 0.55f));
            Bar(ref y, "GYRO", Gyro, Gyro > 0.8f ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.7f, 0.7f, 0.3f));
            if (ShowTimeScale) DrawTimeScale(new Rect(panel.x, panel.yMax + 6, panel.width, 50));

            if (NavBall != null && NavBall.HasData)
            {
                const float ball = 150f;
                Rect area = new Rect(Screen.width * 0.5f - ball * 0.5f, Screen.height - ball - 44f, ball, ball);
                NavBall.Draw(area, _letters);
                GUI.Label(new Rect(area.center.x - 190f, area.yMax + 10f, 380f, 22f), NavCaption, _caption);
            }
            if (!string.IsNullOrEmpty(Others))
            {
                Rect box = new Rect(10, Screen.height - 150, 300, 140);
                GUI.Box(box, GUIContent.none);
                GUI.Label(new Rect(box.x + 8, box.y + 4, box.width - 16, box.height - 8), "OTHERS\n" + Others, _label);
            }
            if (!string.IsNullOrEmpty(Banner))
            {
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 50), Banner, _banner);
            }
            if (ShowHelp && !string.IsNullOrEmpty(Help))
            {
                Rect help = new Rect(Screen.width - 330, 10, 320, 310);
                GUI.Box(help, GUIContent.none);
                GUI.Label(new Rect(help.x + 8, help.y + 6, help.width - 16, help.height - 12), Help, _label);
            }
        }

        // Test tool: log-scaled slider of the time scale (TimeProvider.MinTimeScale .. MaxTimeScale) and a reset to real time.
        // It changes the local game time of the offline flight only; online (stage 2c) it becomes a request to the server for admin sessions.
        private void DrawTimeScale(Rect area)
        {
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x + 8, area.y + 2, area.width - 16, 22), TimeScaleInfo, _label);
            if (RequestTimeScale != null)
            {
                // online admin: the slider shows the pending request or the server scale and sends after a pause
                double shown = double.IsNaN(_pendingScale) ? TimeProvider.TimeScale : _pendingScale;
                float pos = (float) TimeProvider.SliderFromScale(shown);
                float drag = GUI.HorizontalSlider(new Rect(area.x + 8, area.y + 30, area.width - 70, 16), pos, 0f, 1f);
                if (!Mathf.Approximately(drag, pos)) Request(TimeProvider.ScaleFromSlider(drag));
                if (GUI.Button(new Rect(area.xMax - 54, area.y + 25, 46, 20), "1x")) Request(1.0);
                return;
            }
            float position = (float) TimeProvider.SliderFromScale(TimeProvider.TimeScale);
            float moved = GUI.HorizontalSlider(new Rect(area.x + 8, area.y + 30, area.width - 70, 16), position, 0f, 1f);
            if (!Mathf.Approximately(moved, position)) TimeProvider.SetTimeScale(TimeProvider.ScaleFromSlider(moved));
            if (GUI.Button(new Rect(area.xMax - 54, area.y + 25, 46, 20), "1x")) TimeProvider.SetTimeScale(1.0);
        }

        private void Request(double scale)
        {
            _pendingScale = scale;
            _pendingAt = Time.unscaledTime;
        }

        private void Bar(ref float y, string caption, float value, Color color)
        {
            const float x = 18f;
            GUI.Label(new Rect(x, y - 4, 80, 20), caption, _label);
            Rect back = new Rect(x + 90, y, 260, 12);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * Mathf.Clamp01(value), back.height), Texture2D.whiteTexture);
            GUI.color = old;
            y += 20f;
        }
    }
}
