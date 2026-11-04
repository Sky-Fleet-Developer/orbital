using System.Collections.Generic;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>A marker of the map on the screen, GUI coordinates (y down).</summary>
    public struct MapMarker
    {
        public Vector2 Position;
        public float Size;
        public Color Color;
        public string Label;
        public MapMarkerShape Shape;
    }

    public enum MapMarkerShape { Dot, Ring, Diamond, Cross }

    /// <summary>
    /// The IMGUI layer of the map (stage 4): markers and labels at a constant size in pixels, the information window at the cursor and a summary panel. No
    /// data of its own: <see cref="FlightMapView"/> fills it every frame and turns it off with the map.
    /// </summary>
    public sealed class FlightMapOverlay : MonoBehaviour
    {
        public readonly List<MapMarker> Markers = new List<MapMarker>(64);
        /// <summary>The window at the cursor; empty = nothing under it.</summary>
        public string Hover = string.Empty;
        public Vector2 HoverAt;
        /// <summary>The panel: what the map is looking at and what the craft will do.</summary>
        public string Summary = string.Empty;

        private const string KeyPanel = "map";
        private GUIStyle _label;
        private GUIStyle _window;
        private GUIStyle _panel;
        private Texture2D _dot;
        private Texture2D _ring;
        private Texture2D _diamond;
        private Texture2D _cross;

        private void OnGUI()
        {
            if (_label == null) Prepare();
            GUI.depth = 1; // under the flight HUD
            foreach (MapMarker m in Markers)
            {
                Rect at = new Rect(m.Position.x - m.Size * 0.5f, m.Position.y - m.Size * 0.5f, m.Size, m.Size);
                Color old = GUI.color;
                GUI.color = m.Color;
                GUI.DrawTexture(at, TextureOf(m.Shape));
                GUI.color = old;
                if (string.IsNullOrEmpty(m.Label)) continue;
                _label.normal.textColor = m.Color;
                GUI.Label(new Rect(at.xMax + 3f, m.Position.y - 10f, 320f, 20f), m.Label, _label);
            }
            if (!string.IsNullOrEmpty(Summary))
            {
                bool open = !HudPanels.IsCollapsed(KeyPanel);
                Vector2 size = _panel.CalcSize(new GUIContent(Summary));
                float width = open ? Mathf.Max(size.x + 16f, 120f) : 80f;
                float height = open ? HudPanels.HeaderHeight + size.y + 8f : HudPanels.HeaderHeight;
                Rect box = new Rect(Screen.width - width - 10f, Screen.height - height - 10f, width, height);
                GUI.Box(box, GUIContent.none);
                HudPanels.Header(box, KeyPanel, "MAP", _panel);
                if (open) GUI.Label(new Rect(box.x + 8f, box.y + HudPanels.HeaderHeight + 2f, size.x, size.y), Summary, _panel);
            }
            if (!string.IsNullOrEmpty(Hover))
            {
                Vector2 size = _window.CalcSize(new GUIContent(Hover));
                float x = Mathf.Min(HoverAt.x + 16f, Screen.width - size.x - 20f);
                float y = Mathf.Min(HoverAt.y + 16f, Screen.height - size.y - 16f);
                Rect box = new Rect(x, y, size.x + 12f, size.y + 10f);
                GUI.Box(box, GUIContent.none);
                GUI.Box(box, GUIContent.none); // twice: the default box is too transparent over the lines
                GUI.Label(new Rect(box.x + 6f, box.y + 5f, size.x, size.y), Hover, _window);
            }
        }

        private Texture2D TextureOf(MapMarkerShape shape)
        {
            switch (shape)
            {
                case MapMarkerShape.Ring: return _ring;
                case MapMarkerShape.Diamond: return _diamond;
                case MapMarkerShape.Cross: return _cross;
                default: return _dot;
            }
        }

        private void Prepare()
        {
            _label = new GUIStyle(GUI.skin.label) {fontSize = 13, wordWrap = false};
            _window = new GUIStyle(GUI.skin.label) {fontSize = 13, wordWrap = false};
            _window.normal.textColor = Color.white;
            _panel = new GUIStyle(GUI.skin.label) {fontSize = 14, wordWrap = false};
            _panel.normal.textColor = Color.white;
            _dot = Shape((x, y) => 1f - Mathf.Sqrt(x * x + y * y));
            _ring = Shape((x, y) => 1f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.82f) * 6f - 0.25f);
            _diamond = Shape((x, y) => 1f - Mathf.Abs(x) - Mathf.Abs(y));
            _cross = Shape((x, y) => Mathf.Max(0.16f - Mathf.Abs(x - y), 0.16f - Mathf.Abs(x + y)) * 6f);
        }

        // A white shape with a soft edge from a function positive inside, -1..1 across the square.
        private static Texture2D Shape(System.Func<float, float, float> inside)
        {
            const int Size = 32;
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) {wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear};
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                    texture.SetPixel(i, j, new Color(1f, 1f, 1f, Mathf.Clamp01(inside(x, y) * Size * 0.5f)));
                }
            }
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (_dot != null) Destroy(_dot);
            if (_ring != null) Destroy(_ring);
            if (_diamond != null) Destroy(_diamond);
            if (_cross != null) Destroy(_cross);
        }
    }
}
