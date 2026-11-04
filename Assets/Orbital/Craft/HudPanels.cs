using System.Collections.Generic;
using UnityEngine;

namespace Orbital.Craft
{
    /// <summary>
    /// What is common to the folding panels of the HUD and the map (IMGUI): a small button that folds a panel, the state per panel kept between launches, and
    /// the rectangles of this frame, so that a click on a panel does not go through to the map under it.
    /// </summary>
    public static class HudPanels
    {
        public const float HeaderHeight = 22f;
        private const string PrefsPrefix = "orbital.hud.collapsed.";
        private static readonly Dictionary<string, bool> Collapsed = new Dictionary<string, bool>();
        private static readonly List<Rect> Rects = new List<Rect>(16);
        private static int _rectsFrame = -1;

        public static bool IsCollapsed(string key)
        {
            if (!Collapsed.TryGetValue(key, out bool collapsed))
            {
                collapsed = PlayerPrefs.GetInt(PrefsPrefix + key, 0) != 0;
                Collapsed[key] = collapsed;
            }
            return collapsed;
        }

        /// <param name="remember">False for an automated run: the player's own choice stays as it was.</param>
        public static void SetCollapsed(string key, bool collapsed, bool remember = true)
        {
            Collapsed[key] = collapsed;
            if (!remember) return;
            PlayerPrefs.SetInt(PrefsPrefix + key, collapsed ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Draws the folding button at the top left of a panel, with the title after it; true while the panel is unfolded. <paramref name="box"/> is what the panel covers now.</summary>
        public static bool Header(Rect box, string key, string title, GUIStyle titleStyle)
        {
            Report(box);
            bool collapsed = IsCollapsed(key);
            if (GUI.Button(new Rect(box.x + 3f, box.y + 2f, 22f, 18f), collapsed ? "+" : "-"))
            {
                collapsed = !collapsed;
                SetCollapsed(key, collapsed);
            }
            if (!string.IsNullOrEmpty(title)) GUI.Label(new Rect(box.x + 30f, box.y + 1f, box.width - 34f, HeaderHeight), title, titleStyle);
            return !collapsed;
        }

        /// <summary>A rectangle of this frame's HUD. OnGUI runs after Update, so the input of the next frame reads the last frame's list.</summary>
        public static void Report(Rect box)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_rectsFrame != Time.frameCount)
            {
                _rectsFrame = Time.frameCount;
                Rects.Clear();
            }
            Rects.Add(box);
        }

        /// <param name="screen">The cursor, pixels, y up.</param>
        public static bool Covers(Vector2 screen)
        {
            Vector2 gui = new Vector2(screen.x, Screen.height - screen.y);
            foreach (Rect box in Rects)
            {
                if (box.Contains(gui)) return true;
            }
            return false;
        }
    }
}
