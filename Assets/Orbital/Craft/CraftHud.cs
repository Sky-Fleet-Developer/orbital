using Orbital.Core;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>
    /// Greybox HUD: folding panels (<see cref="HudPanels"/>), drawn with IMGUI. The project has no UI of its own yet, and IMGUI needs no assets, no canvas and no
    /// scene change (the spawner creates this component), so it is the simplest thing that works; the cockpit replaces it later.
    /// The spawner fills the public fields a few times per second.
    /// </summary>
    public sealed class CraftHud : MonoBehaviour
    {
        public string Telemetry = string.Empty;
        public string Banner = string.Empty;
        public string Help = string.Empty;
        /// <summary>The help panel unfolded (F1 and its own button).</summary>
        public bool ShowHelp
        {
            get => !HudPanels.IsCollapsed(KeyHelp);
            set => HudPanels.SetCollapsed(KeyHelp, !value);
        }
        private const string KeyNet = "net";
        private const string KeyFlight = "flight";
        private const string KeyNav = "nav";
        private const string KeyOthers = "others";
        private const string KeyDescent = "descent";
        private const string KeyHelp = "help";
        private const string KeyTime = "time";
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
        /// <summary>The descent instrument (stage 4.1): its lines, empty when the craft is not coming down.</summary>
        public string Descent = string.Empty;
        /// <summary>The first line of the instrument in big letters: what to do now.</summary>
        public string DescentCall = string.Empty;
        public Color DescentColor = Color.white;
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
        private GUIStyle _descentCall;

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
                _descentCall = new GUIStyle(GUI.skin.label) {fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = false};
            }
            Rect panel = new Rect(10, 10, 400, 0);
            if (!string.IsNullOrEmpty(NetLine))
            {
                bool netOpen = !HudPanels.IsCollapsed(KeyNet);
                Rect net = new Rect(panel.x, panel.y, netOpen ? 760f : 80f, HudPanels.HeaderHeight + 2f);
                GUI.Box(net, GUIContent.none);
                HudPanels.Header(net, KeyNet, netOpen ? NetLine : "NET", _label);
                panel.y += net.height + 4f;
            }
            bool flightOpen = !HudPanels.IsCollapsed(KeyFlight);
            panel.height = flightOpen ? HudPanels.HeaderHeight + 380f : HudPanels.HeaderHeight;
            GUI.Box(panel, GUIContent.none);
            HudPanels.Header(panel, KeyFlight, "FLIGHT", _label);
            if (flightOpen)
            {
                float top = panel.y + HudPanels.HeaderHeight;
                GUI.Label(new Rect(panel.x + 8, top + 6, panel.width - 16, 282), Telemetry, _label);
                float y = top + 292;
                Bar(ref y, panel.x, "THR", Throttle, new Color(1f, 0.6f, 0.15f));
                Bar(ref y, panel.x, "FUEL", MainFuel, new Color(0.3f, 0.8f, 0.35f));
                Bar(ref y, panel.x, RcsOn ? "RCS on" : "RCS off", RcsFuel, RcsOn ? new Color(0.35f, 0.75f, 1f) : new Color(0.45f, 0.5f, 0.55f));
                Bar(ref y, panel.x, "GYRO", Gyro, Gyro > 0.8f ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.7f, 0.7f, 0.3f));
            }
            if (ShowTimeScale) DrawTimeScale(new Rect(panel.x, panel.yMax + 6, panel.width, 0));

            if (NavBall != null && NavBall.HasData)
            {
                const float ball = 150f;
                bool navOpen = !HudPanels.IsCollapsed(KeyNav);
                Rect area = new Rect(Screen.width * 0.5f - ball * 0.5f, Screen.height - ball - 44f, ball, ball);
                if (navOpen) NavBall.Draw(area, _letters);
                Rect row = new Rect(area.center.x - 190f, area.yMax + 10f, 380f, HudPanels.HeaderHeight);
                HudPanels.Report(area);
                HudPanels.Header(row, KeyNav, string.Empty, _label);
                GUI.Label(row, NavCaption, _caption);
            }
            if (!string.IsNullOrEmpty(Others))
            {
                bool othersOpen = !HudPanels.IsCollapsed(KeyOthers);
                float height = othersOpen ? HudPanels.HeaderHeight + 124f : HudPanels.HeaderHeight;
                Rect box = new Rect(10, Screen.height - height - 10f, 300, height);
                GUI.Box(box, GUIContent.none);
                HudPanels.Header(box, KeyOthers, "OTHERS", _label);
                if (othersOpen) GUI.Label(new Rect(box.x + 8, box.y + HudPanels.HeaderHeight + 2f, box.width - 16, 118f), Others, _label);
            }
            if (!string.IsNullOrEmpty(Descent)) DrawDescent();
            if (!string.IsNullOrEmpty(Banner))
            {
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 50), Banner, _banner);
            }
            if (!string.IsNullOrEmpty(Help))
            {
                bool helpOpen = !HudPanels.IsCollapsed(KeyHelp);
                Rect help = new Rect(Screen.width - 330, 10, 320, helpOpen ? HudPanels.HeaderHeight + 310f : HudPanels.HeaderHeight);
                GUI.Box(help, GUIContent.none);
                HudPanels.Header(help, KeyHelp, "CONTROLS", _label);
                if (helpOpen) GUI.Label(new Rect(help.x + 8, help.y + HudPanels.HeaderHeight + 2f, help.width - 16, 304f), Help, _label);
            }
        }

        // Folded it still says what to do now: that line is the point of the instrument.
        private void DrawDescent()
        {
            const float width = 300f;
            bool open = !HudPanels.IsCollapsed(KeyDescent);
            float height = open ? 172f : 34f;
            Rect box = new Rect(Screen.width * 0.5f + 90f, Screen.height - height - 14f, width, height);
            GUI.Box(box, GUIContent.none);
            GUI.Box(box, GUIContent.none); // twice: over the map the default box is too transparent
            HudPanels.Header(box, KeyDescent, string.Empty, _label);
            _descentCall.normal.textColor = DescentColor;
            GUI.Label(new Rect(box.x + 30f, box.y + 2f, width - 34f, 30f), DescentCall, _descentCall);
            if (open) GUI.Label(new Rect(box.x + 8f, box.y + 36f, width - 16f, height - 40f), Descent, _label);
        }

        // Test tool: log-scaled slider of the time scale (TimeProvider.MinTimeScale .. MaxTimeScale) and a reset to real time.
        // It changes the local game time of the offline flight only; online (stage 2c) it becomes a request to the server for admin sessions.
        private void DrawTimeScale(Rect area)
        {
            bool open = !HudPanels.IsCollapsed(KeyTime);
            area.height = open ? 50f : HudPanels.HeaderHeight;
            GUI.Box(area, GUIContent.none);
            HudPanels.Header(area, KeyTime, open ? TimeScaleInfo : "TIME", _label);
            if (!open) return;
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

        private void Bar(ref float y, float panelX, string caption, float value, Color color)
        {
            float x = panelX + 8f;
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
