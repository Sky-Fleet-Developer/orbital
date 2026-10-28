using System;
using Orbital.Core.Flight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.Craft
{
    /// <summary>
    /// The navigation ball of the greybox cockpit. The math (local horizon, cockpit axes, markers, angles) and the picture itself are the engine-free
    /// <see cref="NavBall"/> and <see cref="NavBallRaster"/> in Core/Flight, covered by tests; this class only uploads the picture to a texture and
    /// draws it with IMGUI together with the markers (prograde, retrograde, SAS target, the fixed craft mark and the letters N E S W).
    /// No camera, layer, mesh, shader or asset is involved: the ball is rasterised on the CPU into a small texture a few times per second, all textures
    /// of the markers are made in code. Picture convention: see <see cref="NavBallData"/> (center = where the nose points, right = the pilot's right).
    /// </summary>
    public sealed class NavBallView : IDisposable
    {
        public const int Size = 128;           // pixels of the ball texture
        private const float RenderInterval = 0.05f; // seconds between two rasterisations (about 20 per second)

        private static readonly Color ProgradeColor = new Color(0.72f, 1f, 0.15f);
        private static readonly Color TargetColor = new Color(0.25f, 0.9f, 1f);
        private static readonly Color CraftColor = new Color(1f, 0.6f, 0.1f);

        private readonly Texture2D _ball;
        private readonly byte[] _buffer = new byte[NavBallRaster.BufferLength(Size)];
        private readonly Texture2D _backdrop;
        private readonly Texture2D _prograde;
        private readonly Texture2D _retrograde;
        private readonly Texture2D _target;
        private readonly Texture2D _craftMark;
        private NavBallData _data;
        private float _nextRender;

        /// <summary>False until the first <see cref="Update"/>.</summary>
        public bool HasData { get; private set; }

        /// <summary>Latest numbers (angles, speed, mode) for the text of the HUD.</summary>
        public NavBallData Data => _data;

        public NavBallView()
        {
            _ball = new Texture2D(Size, Size, TextureFormat.RGBA32, false) {name = "NavBall", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp};
            _backdrop = Make(64, 64, (x, y) => Disc(x, y, 31.5f), new Color(0.05f, 0.05f, 0.07f, 0.85f));
            _prograde = Make(32, 32, (x, y) => Mathf.Max(Ring(x, y, 10f, 1.3f), Disc(x, y, 2.4f), Bar(x, y, 10f, 15f, 1.3f)), ProgradeColor);
            _retrograde = Make(32, 32, (x, y) => Mathf.Max(Ring(x, y, 10f, 1.3f), Cross(x, y, 7f, 1.3f)), ProgradeColor);
            _target = Make(32, 32, (x, y) => Diamond(x, y, 11f, 1.4f), TargetColor);
            _craftMark = Make(48, 32, (x, y) => Mathf.Max(Ring(x, y, 5f, 1.4f), Bar(x, y, 9f, 21f, 1.6f), Stub(x, y, 5f, 10f, 1.6f)), CraftColor);
        }

        /// <summary>Recomputes the ball for the state; rasterises it at most about 20 times a second.</summary>
        public void Update(in FlightState state, in SurfaceData surface, bool orbitMode)
        {
            _data = NavBall.Compute(state, surface, orbitMode);
            HasData = true;
            if (Time.unscaledTime < _nextRender) return;
            _nextRender = Time.unscaledTime + RenderInterval;
            NavBallRaster.Render(_data, Size, _buffer);
            _ball.LoadRawTextureData(_buffer);
            _ball.Apply(false);
        }

        /// <summary>Draws the ball in the square <paramref name="area"/> (IMGUI, call from OnGUI). <paramref name="letters"/> is a centered label style.</summary>
        public void Draw(Rect area, GUIStyle letters)
        {
            if (!HasData) return;
            Color oldColor = GUI.color;
            Color oldContent = GUI.contentColor;
            float radius = area.width * 0.5f;
            Vector2 center = area.center;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(area.x - 7f, area.y - 7f, area.width + 14f, area.height + 14f), _backdrop, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(area, _ball, ScaleMode.StretchToFill, true);

            // letters on the horizon
            DrawLetter(center, radius, new Vec3(0, 1, 0), "N", new Color(1f, 0.35f, 0.3f), letters);
            DrawLetter(center, radius, new Vec3(1, 0, 0), "E", Color.white, letters);
            DrawLetter(center, radius, new Vec3(0, -1, 0), "S", Color.white, letters);
            DrawLetter(center, radius, new Vec3(-1, 0, 0), "W", Color.white, letters);

            if (_data.HasVelocity)
            {
                DrawMarker(center, radius, _data.Prograde, _prograde, 1f);
                DrawMarker(center, radius, _data.Retrograde, _retrograde, 1f);
            }
            if (_data.HasSasTarget) DrawMarker(center, radius, _data.SasTargetNose, _target, 1f);

            // the craft itself, fixed in the middle
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - 24f, center.y - 16f, 48f, 32f), _craftMark, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
            GUI.contentColor = oldContent;
        }

        // A marker on the front hemisphere (facing the viewer); markers behind the ball are not drawn.
        private void DrawMarker(Vector2 center, float radius, Vec3 direction, Texture2D texture, float alpha)
        {
            Vec3 view = NavBall.ToView(_data, direction);
            if (view.Z >= 0.0) return;
            float x = center.x + (float) view.X * radius;
            float y = center.y - (float) view.Y * radius;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(x - 16f, y - 16f, 32f, 32f), texture, ScaleMode.StretchToFill, true);
        }

        private void DrawLetter(Vector2 center, float radius, Vec3 direction, string text, Color color, GUIStyle style)
        {
            Vec3 view = NavBall.ToView(_data, direction);
            if (view.Z >= -0.1) return;
            float x = center.x + (float) view.X * radius * 0.92f;
            float y = center.y - (float) view.Y * radius * 0.92f;
            GUI.contentColor = color;
            GUI.Label(new Rect(x - 12f, y - 10f, 24f, 20f), text, style);
        }

        public void Dispose()
        {
            foreach (Texture2D texture in new[] {_ball, _backdrop, _prograde, _retrograde, _target, _craftMark})
            {
                if (texture != null) Object.Destroy(texture);
            }
        }

        // ------------------------------------------------------------------ procedural marker textures
        // Each shape is a function of the offset (pixels) from the center of the texture; the value is the coverage 0..1 (1 pixel anti-aliasing).

        private static Texture2D Make(int width, int height, Func<float, float, float> coverage, Color color)
        {
            Color32[] pixels = new Color32[width * height];
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    float x = i + 0.5f - width * 0.5f;
                    float y = j + 0.5f - height * 0.5f;
                    float a = Mathf.Clamp01(coverage(x, y));
                    pixels[j * width + i] = new Color32((byte) (color.r * 255f), (byte) (color.g * 255f), (byte) (color.b * 255f), (byte) (a * color.a * 255f));
                }
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        private static float Disc(float x, float y, float radius) => Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(x * x + y * y));

        private static float Ring(float x, float y, float radius, float halfWidth) =>
            Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - radius));

        // two horizontal bars on both sides of the center, between |x| = from and to
        private static float Bar(float x, float y, float from, float to, float halfWidth)
        {
            float ax = Mathf.Abs(x);
            if (ax < from - 0.5f || ax > to + 0.5f) return 0f;
            return Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(y));
        }

        // a short vertical bar below the center, between y = -to and y = -from
        private static float Stub(float x, float y, float from, float to, float halfWidth)
        {
            if (y > -from + 0.5f || y < -to - 0.5f) return 0f;
            return Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(x));
        }

        // an X inside the circle of the given radius
        private static float Cross(float x, float y, float radius, float halfWidth)
        {
            if (x * x + y * y > (radius + 0.5f) * (radius + 0.5f)) return 0f;
            float d = Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x + y)) * 0.70710678f;
            return Mathf.Clamp01(halfWidth + 0.5f - d);
        }

        private static float Diamond(float x, float y, float radius, float halfWidth) =>
            Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(Mathf.Abs(x) + Mathf.Abs(y) - radius) * 0.70710678f);
    }
}
