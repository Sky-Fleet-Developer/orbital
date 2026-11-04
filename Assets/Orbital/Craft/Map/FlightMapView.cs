using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ara3D;
using Ara3D.Double;
using Orbital.Core.Flight;
using Orbital.Core.TrajectorySystem;
using Orbital.View.Flight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.Craft
{
    /// <summary>What the map needs to know about the own craft this frame.</summary>
    public struct MapCraft
    {
        public bool Exists;
        public string Callsign;
        public int Parent;
        public Vec3 Position;
        public Vec3 Velocity;
        public int Mode;
        public double Altitude;
    }

    /// <summary>
    /// The map of the flight (stage 4): the whole table of bodies, their orbits and the forecast of the craft. Nothing is kept in float: every position is the
    /// difference of doubles from the point the camera looks at, scaled so that this point is <see cref="CameraUnits"/> units from the camera, and the
    /// meshes are rewritten every frame. So the scale can run from a craft on the ground to the whole system with the same precision. The orbit of a body
    /// is sampled from where the body is now, densest there, so the line passes exactly through the body at any zoom.
    /// </summary>
    public sealed class FlightMapView : IDisposable
    {
        private const float CameraUnits = 100f;
        private const int OrbitSamples = 257;
        private const double MinDistance = 200.0;
        private const double MinApsisEccentricity = 0.002;
        private const double MaxDistance = 2e14;

        private readonly BodyFrameTable _frames;
        private readonly BodyLook[] _looks;
        private readonly Func<string, string> _nameOf;
        private readonly MapViewSettings _settings;
        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly Mesh _sphere;
        private readonly Material[] _bodyMaterials;
        private readonly Transform[] _bodies;
        private readonly MapLine[] _orbits;
        private readonly MapLine[] _trajectory = new MapLine[TrajectoryPlot.MaxSegments];
        private readonly FlightMapOverlay _overlay;
        private readonly Vec3[] _global;
        private readonly Vector3[] _points = new Vector3[Math.Max(OrbitSamples, TrajectoryPlot.SamplesPerSegment)];
        private readonly Vector2[][] _screenLine = new Vector2[TrajectoryPlot.MaxSegments][];
        private readonly bool[][] _screenLineOk = new bool[TrajectoryPlot.MaxSegments][];
        private readonly StringBuilder _text = new StringBuilder(256);
        private readonly int _sun;

        private bool _active;
        private int _focusBody = -1; // -1 = the craft
        private double _distance = 3e7;
        private float _yaw = 20f;
        private float _pitch = 55f;
        private bool _fitted;
        private double _time;
        private double _unit;
        private Vec3 _focus;
        private float _pixelsPerUnitAtOne;
        private Matrix4x4 _viewProjection;
        private int _hoverBody = -1;
        private bool _hoverSelf;

        public bool Active => _active;
        /// <summary>The body the forecast is drawn around: the focused one, or the craft's own.</summary>
        public int FrameBodyFor(int craftParent) => _focusBody >= 0 ? _focusBody : craftParent;

        public FlightMapView(BodyFrameTable frames, BodyLook[] looks, Func<string, string> nameOf, MapViewSettings settings, Transform parent, float depth)
        {
            _frames = frames;
            _looks = looks;
            _nameOf = nameOf;
            _settings = settings;
            int count = frames.Count;
            _global = new Vec3[count];
            _bodies = new Transform[count];
            _bodyMaterials = new Material[count];
            _orbits = new MapLine[count];

            _root = new GameObject("[FlightMap]");
            _root.transform.SetParent(parent, false);
            GameObject cameraObject = new GameObject("MapCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.cullingMask = 1 << settings.layer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 1e9f;
            _camera.depth = depth;
            _camera.enabled = false;

            _sphere = ScaledSpaceView.BuildSphere(64, 32);
            _sun = -1;
            Shader lineShader = Resources.Load<Shader>("Orbital/MapLine");
            if (lineShader == null) throw new InvalidOperationException("FlightMapView: no shader Resources/Orbital/MapLine");
            for (int i = 0; i < count; i++)
            {
                if (_sun < 0 && looks[i].Star) _sun = i;
                if (frames[i].Parent >= 0) _orbits[i] = new MapLine("Orbit " + frames.IdOf(i), OrbitSamples, lineShader, settings.layer, _root.transform);
                if (frames[i].Virtual || frames[i].Radius <= 0) continue;
                _bodyMaterials[i] = ScaledSpaceView.NewBodyMaterial(looks[i].Tint, looks[i].Emission, true);
                GameObject body = new GameObject("Body " + frames.IdOf(i), typeof(MeshFilter), typeof(MeshRenderer));
                body.layer = settings.layer;
                body.transform.SetParent(_root.transform, false);
                body.GetComponent<MeshFilter>().sharedMesh = _sphere;
                MeshRenderer renderer = body.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _bodyMaterials[i];
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                _bodies[i] = body.transform;
            }
            for (int i = 0; i < _trajectory.Length; i++)
            {
                _trajectory[i] = new MapLine("Trajectory " + i, TrajectoryPlot.SamplesPerSegment, lineShader, settings.layer, _root.transform);
                _screenLine[i] = new Vector2[TrajectoryPlot.SamplesPerSegment];
                _screenLineOk[i] = new bool[TrajectoryPlot.SamplesPerSegment];
            }
            GameObject overlayObject = new GameObject("MapOverlay");
            overlayObject.transform.SetParent(_root.transform, false);
            _overlay = overlayObject.AddComponent<FlightMapOverlay>();
            _root.SetActive(false);
        }

        public void SetActive(bool active)
        {
            _active = active;
            _camera.enabled = active;
            _root.SetActive(active);
        }

        /// <summary>F: the camera follows the craft, in the frame of its body. The first time and on request the whole orbit is fitted to the view.</summary>
        public void FocusCraft()
        {
            _focusBody = -1;
            _fitted = false;
        }

        /// <summary>Follows a body; the distance stays unless the body does not fit.</summary>
        public void FocusBody(int body)
        {
            _focusBody = body;
            double radius = _frames[body].Radius;
            double soi = _frames[body].SoiRadius;
            if (!double.IsInfinity(soi)) _distance = Math.Min(_distance, 3.0 * soi);
            _distance = Math.Max(_distance, 4.0 * radius);
            _fitted = true;
        }

        public void SetDistance(double meters)
        {
            _distance = Clamp(meters, MinDistance, MaxDistance);
            _fitted = true;
        }

        public void Control(Vector2 drag, float zoomNotches, bool clicked, bool focusCraft)
        {
            if (focusCraft) FocusCraft();
            _yaw = Mathf.Repeat(_yaw + drag.x * _settings.dragSpeed, 360f);
            _pitch = Mathf.Clamp(_pitch - drag.y * _settings.dragSpeed, -89f, 89f);
            if (zoomNotches != 0f) _distance = Clamp(_distance * Math.Pow(_settings.zoomPerNotch, zoomNotches), MinDistance, MaxDistance);
            if (!clicked) return;
            if (_hoverBody >= 0) FocusBody(_hoverBody);
            else if (_hoverSelf) FocusCraft();
        }

        /// <param name="mouse">The cursor, screen coordinates (y up).</param>
        public void Present(double time, in MapCraft craft, TrajectoryPlot plot, IReadOnlyDictionary<string, CraftNetLink.Other> others, Vector2 mouse)
        {
            if (!_active) return;
            _time = time;
            for (int i = 0; i < _global.Length; i++) _frames.GlobalState(i, time, out _global[i], out _);
            Vec3 craftGlobal = craft.Exists ? _global[craft.Parent] + craft.Position : Vec3.Zero;
            // no craft yet (online, before the server gives it): look at the root for now, the craft is still the focus when it comes
            int looked = _focusBody >= 0 || craft.Exists ? _focusBody : 0;
            _focus = looked >= 0 ? _global[looked] : craftGlobal;
            if (!_fitted && craft.Exists && _focusBody < 0) FitCraft(craft, plot);
            double minimum = looked >= 0 ? Math.Max(MinDistance, 1.1 * _frames[looked].Radius) : MinDistance;
            if (_distance < minimum) _distance = minimum;
            _unit = CameraUnits / _distance;

            Transform eye = _camera.transform;
            eye.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            eye.position = eye.rotation * new Vector3(0f, 0f, -CameraUnits);
            _camera.fieldOfView = _settings.fieldOfView;
            _camera.backgroundColor = _settings.background;
            _camera.nearClipPlane = 1f;
            _viewProjection = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            _pixelsPerUnitAtOne = _camera.pixelHeight * 0.5f / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            _overlay.Markers.Clear();
            _hoverBody = -1;
            _hoverSelf = false;
            string hover = null;
            Vector2 guiMouse = new Vector2(mouse.x, _camera.pixelHeight - mouse.y);

            PresentBodies(craft, mouse, ref hover);
            PresentTrajectory(time, plot, mouse, ref hover);
            TryScreen(ToMap(craftGlobal), out Vector2 craftScreen);
            PresentEvents(time, plot, craftScreen, mouse, ref hover);
            if (others != null) PresentOthers(time, others, craft, craftGlobal, mouse, ref hover);
            PresentCraft(time, craft, plot, craftGlobal, mouse, ref hover);

            _overlay.Hover = hover ?? string.Empty;
            _overlay.HoverAt = guiMouse;
            _overlay.Summary = Summary(time, craft, plot);
        }

        private void FitCraft(in MapCraft craft, TrajectoryPlot plot)
        {
            double extent = Math.Max(Vec3.Length(craft.Position), 1.5 * _frames[craft.Parent].Radius);
            if (plot.Count > 0 && plot.Parent == craft.Parent)
            {
                double ap = plot.Segments[0].Orbit.Apocenter;
                if (!double.IsInfinity(ap)) extent = Math.Max(extent, ap);
                else if (!double.IsInfinity(_frames[craft.Parent].SoiRadius)) extent = Math.Max(extent, _frames[craft.Parent].SoiRadius);
            }
            _distance = Clamp(3.2 * extent, MinDistance, MaxDistance);
            if (plot.Count > 0) _fitted = true;
        }

        // ------------------------------------------------------------------ bodies

        private void PresentBodies(in MapCraft craft, Vector2 mouse, ref string hover)
        {
            Vec3 sun = _sun >= 0 ? _global[_sun] : Vec3.Zero;
            for (int i = 0; i < _global.Length; i++)
            {
                ref readonly BodyFrame frame = ref _frames[i];
                Vector3 at = ToMap(_global[i]);
                bool important = i == _focusBody || (craft.Exists && i == craft.Parent);
                bool shown = frame.Parent < 0 || important;
                if (frame.Parent >= 0)
                {
                    double pixels = OrbitPixels(frame.Orbit.SemiMajorAxis, at);
                    shown |= pixels >= _settings.minOrbitPixels;
                    PresentOrbit(i, shown);
                }
                Transform body = _bodies[i];
                if (body == null) continue;
                float radius = (float) (frame.Radius * _unit);
                float depth = Vector3.Dot(at - _camera.transform.position, _camera.transform.forward);
                float radiusPixels = depth > 0f ? radius * _pixelsPerUnitAtOne / depth : 0f;
                bool drawn = shown && radiusPixels >= 0.5f;
                if (body.gameObject.activeSelf != drawn) body.gameObject.SetActive(drawn);
                if (drawn)
                {
                    body.position = at;
                    body.localScale = Vector3.one * radius;
                    if (_sun >= 0) ScaledSpaceView.SetSunDirection(_bodyMaterials[i], SunLight.Direction(sun, _global[i]));
                }
                if (!shown || !TryScreen(at, out Vector2 screen)) continue;
                Color color = _looks[i].Star ? new Color(1f, 0.9f, 0.6f) : Bright(_looks[i].Tint);
                _overlay.Markers.Add(new MapMarker
                {
                    Position = Gui(screen), Size = radiusPixels < _settings.minBodyPixels ? 7f : 0f, Color = color,
                    Label = NameOf(i), Shape = MapMarkerShape.Dot,
                });
                if (hover == null && Vector2.Distance(screen, mouse) <= Mathf.Max(radiusPixels, _settings.hoverPixels))
                {
                    _hoverBody = i;
                    hover = BodyText(i, craft);
                }
            }
        }

        private double OrbitPixels(double semiMajorAxis, Vector3 bodyAt)
        {
            float depth = Mathf.Max(CameraUnits * 0.01f, Vector3.Dot(bodyAt - _camera.transform.position, _camera.transform.forward));
            return semiMajorAxis * _unit * _pixelsPerUnitAtOne / depth;
        }

        // Sampled from the body's anomaly now, denser near the body, where the view may be close enough to see the chord. The middle sample is the body itself.
        private void PresentOrbit(int i, bool shown)
        {
            MapLine line = _orbits[i];
            line.SetVisible(shown);
            if (!shown) return;
            ref readonly BodyFrame frame = ref _frames[i];
            Vec3 parent = _global[frame.Parent];
            _frames.LocalState(i, _time, out Vec3 local, out _);
            StaticOrbit orbit = frame.Orbit;
            DMatrix4x4 matrix = orbit.RotationMatrix;
            DVector3 forward = matrix.Forward(), right = matrix.Right();
            Vec3 fwd = new Vec3(forward.x, forward.y, forward.z), rgt = new Vec3(right.x, right.y, right.z);
            double now = Math.Atan2(Vec3.Dot(local, rgt), Vec3.Dot(local, fwd));
            double p = orbit.SemiLatusRectum, e = orbit.Eccentricity;
            for (int k = 0; k < OrbitSamples; k++)
            {
                double s = 2.0 * k / (OrbitSamples - 1) - 1.0;
                double nu = now + Math.PI * (0.35 * s + 0.65 * s * s * s);
                double cos = Math.Cos(nu), sin = Math.Sin(nu);
                double r = p / (1.0 + e * cos);
                _points[k] = ToMap(parent + fwd * (r * cos) + rgt * (r * sin));
            }
            Color color = Bright(_looks[i].Tint);
            color.a = _settings.orbitOpacity;
            line.Set(_points, color, _settings.orbitWidth);
        }

        // ------------------------------------------------------------------ the forecast

        private void PresentTrajectory(double time, TrajectoryPlot plot, Vector2 mouse, ref string hover)
        {
            Vec3 frame = plot.FrameBody >= 0 ? _global[plot.FrameBody] : Vec3.Zero;
            float best = _settings.hoverPixels;
            int bestSegment = -1, bestSample = -1;
            for (int s = 0; s < _trajectory.Length; s++)
            {
                MapLine line = _trajectory[s];
                bool shown = s < plot.Count;
                line.SetVisible(shown);
                if (!shown) continue;
                Vec3[] relative = plot.Relative[s];
                for (int k = 0; k < relative.Length; k++)
                {
                    _points[k] = ToMap(frame + relative[k]);
                    _screenLineOk[s][k] = TryScreen(_points[k], out _screenLine[s][k]);
                    if (!_screenLineOk[s][k]) continue;
                    float d = Vector2.Distance(_screenLine[s][k], mouse);
                    if (d < best)
                    {
                        best = d;
                        bestSegment = s;
                        bestSample = k;
                    }
                }
                Color[] colors = _settings.trajectoryColors;
                Color color = colors != null && colors.Length > 0 ? colors[Mathf.Min(s, colors.Length - 1)] : Color.green;
                line.Set(_points, color, _settings.trajectoryWidth);
            }
            if (hover == null && bestSegment >= 0) hover = TrajectoryText(time, plot, bestSegment, bestSample);
        }

        private void PresentEvents(double time, TrajectoryPlot plot, Vector2 craftScreen, Vector2 mouse, ref string hover)
        {
            Vec3 frame = plot.FrameBody >= 0 ? _global[plot.FrameBody] : Vec3.Zero;
            for (int i = 0; i < plot.EventCount; i++)
            {
                ref readonly TrajectoryEvent e = ref plot.Events[i];
                if (e.Time < time) continue;
                if (!TryScreen(ToMap(frame + e.Relative), out Vector2 screen)) continue;
                bool apsis = e.Kind == TrajectoryEventKind.Pericenter || e.Kind == TrajectoryEventKind.Apocenter;
                // on a near circle the apsides mean nothing and jump around; from far away they lie on top of the craft
                if (apsis && (plot.Segments[e.Segment].Orbit.Eccentricity < MinApsisEccentricity || Vector2.Distance(screen, craftScreen) < _settings.hoverPixels * 2f)) continue;
                bool impact = e.Kind == TrajectoryEventKind.Impact;
                _overlay.Markers.Add(new MapMarker
                {
                    Position = Gui(screen), Size = impact ? 14f : 9f, Color = impact ? _settings.impactColor : _settings.eventColor,
                    Label = EventLabel(e), Shape = impact ? MapMarkerShape.Cross : MapMarkerShape.Diamond,
                });
                if (hover == null && Vector2.Distance(screen, mouse) <= _settings.hoverPixels) hover = EventText(time, plot, e);
                if (!e.HasBodyGhost || !TryScreen(ToMap(frame + e.BodyGhost), out Vector2 ghost)) continue;
                Color color = Bright(_looks[e.Body].Tint);
                color.a = 0.8f;
                _overlay.Markers.Add(new MapMarker {Position = Gui(ghost), Size = 12f, Color = color, Label = NameOf(e.Body) + " then", Shape = MapMarkerShape.Ring});
            }
        }

        private void PresentCraft(double time, in MapCraft craft, TrajectoryPlot plot, Vec3 craftGlobal, Vector2 mouse, ref string hover)
        {
            if (!craft.Exists || !TryScreen(ToMap(craftGlobal), out Vector2 screen)) return;
            _overlay.Markers.Add(new MapMarker {Position = Gui(screen), Size = 13f, Color = _settings.craftColor, Label = craft.Callsign, Shape = MapMarkerShape.Diamond});
            if (Vector2.Distance(screen, mouse) > _settings.hoverPixels) return;
            _hoverSelf = true;
            _hoverBody = -1;
            hover = CraftText(time, craft, plot);
        }

        private void PresentOthers(double time, IReadOnlyDictionary<string, CraftNetLink.Other> others, in MapCraft craft, Vec3 craftGlobal, Vector2 mouse, ref string hover)
        {
            foreach (KeyValuePair<string, CraftNetLink.Other> kv in others)
            {
                Core.Net.Ghost g = kv.Value.Ghost;
                if (!g.HasState || g.Parent < 0 || g.Parent >= _global.Length) continue;
                Vec3 at = _global[g.Parent] + g.ShownPosition;
                if (!TryScreen(ToMap(at), out Vector2 screen)) continue;
                string name = g.Callsign ?? g.CraftId;
                _overlay.Markers.Add(new MapMarker {Position = Gui(screen), Size = 10f, Color = _settings.otherCraftColor, Label = name, Shape = MapMarkerShape.Diamond});
                if (hover != null || Vector2.Distance(screen, mouse) > _settings.hoverPixels) continue;
                _text.Clear();
                _text.Append(name).Append("\naround ").Append(NameOf(g.Parent)).Append("  ").Append(ModeName(g.ServerMode));
                _text.Append("\nheight ").Append(Length(Vec3.Length(g.ShownPosition) - _frames[g.Parent].Radius));
                if (craft.Exists) _text.Append("\nfrom you ").Append(Length(Vec3.Length(at - craftGlobal)));
                hover = _text.ToString();
            }
        }

        // ------------------------------------------------------------------ text

        private string Summary(double time, in MapCraft craft, TrajectoryPlot plot)
        {
            _text.Clear();
            _text.Append("Looking at ").Append(_focusBody >= 0 ? NameOf(_focusBody) : craft.Exists ? "craft" : "no craft yet").Append("  view ").Append(Length(_distance)).Append('\n');
            if (craft.Exists)
            {
                _text.Append("Around ").Append(NameOf(craft.Parent));
                if (plot.Count > 0 && plot.Parent == craft.Parent)
                {
                    ref readonly TrajectorySegment s = ref plot.Segments[0];
                    double radius = _frames[s.Body].Radius;
                    _text.Append("  Pe ").Append(Length(s.Orbit.Pericenter - radius));
                    _text.Append(s.Orbit.Eccentricity < 1.0 ? "  Ap " + Length(s.Orbit.Apocenter - radius) : "  open");
                    if (s.Orbit.Eccentricity < 1.0) _text.Append("  period ").Append(Duration(s.Orbit.Period));
                    _text.Append('\n');
                    for (int i = 0; i < plot.Count; i++)
                    {
                        ref readonly TrajectorySegment seg = ref plot.Segments[i];
                        string line = EndText(seg);
                        if (line == null || seg.End < time) continue;
                        _text.Append(line).Append(" in ").Append(Duration(seg.End - time)).Append('\n');
                    }
                }
                else _text.Append(craft.Mode == FlightMode.Flying ? "  (no conic: straight up or down)\n" : "  " + ModeName(craft.Mode) + "\n");
            }
            _text.Append("Wheel zoom, RMB turn, click a body to follow it, F the craft");
            return _text.ToString();
        }

        private string EndText(in TrajectorySegment s)
        {
            switch (s.EndKind)
            {
                case TrajectoryEnd.EnterBody: return "Enters " + NameOf(s.NextBody);
                case TrajectoryEnd.LeaveBody: return "Leaves " + NameOf(s.Body) + " for " + NameOf(s.NextBody);
                case TrajectoryEnd.Impact: return "IMPACT on " + NameOf(s.Body);
                default: return null;
            }
        }

        private string EventLabel(in TrajectoryEvent e)
        {
            switch (e.Kind)
            {
                case TrajectoryEventKind.Pericenter: return "Pe " + Length(e.Altitude);
                case TrajectoryEventKind.Apocenter: return "Ap " + Length(e.Altitude);
                case TrajectoryEventKind.EnterBody: return "Enter " + NameOf(e.Body);
                case TrajectoryEventKind.LeaveBody: return "Leave for " + NameOf(e.Body);
                default: return "Impact";
            }
        }

        private string EventText(double time, TrajectoryPlot plot, in TrajectoryEvent e)
        {
            ref readonly TrajectorySegment s = ref plot.Segments[e.Segment];
            return EventLabel(e) + "\naround " + NameOf(s.Body) + "\nin " + Duration(e.Time - time);
        }

        private string TrajectoryText(double time, TrajectoryPlot plot, int segment, int sample)
        {
            ref readonly TrajectorySegment s = ref plot.Segments[segment];
            double r = s.Orbit.RadiusAtTrueAnomaly(plot.Anomalies[segment][sample]);
            double t = plot.Times[segment][sample];
            _text.Clear();
            _text.Append("Trajectory\naround ").Append(NameOf(s.Body));
            _text.Append(t >= time ? "\nin " + Duration(t - time) : "\npassed");
            _text.Append("\nheight ").Append(Length(r - _frames[s.Body].Radius));
            _text.Append("\nspeed ").Append(Speed(s.Orbit.GetOrbitalSpeedAtDistance(r)));
            return _text.ToString();
        }

        private string BodyText(int i, in MapCraft craft)
        {
            ref readonly BodyFrame b = ref _frames[i];
            _text.Clear();
            _text.Append(NameOf(i));
            if (b.Parent >= 0) _text.Append("\naround ").Append(NameOf(b.Parent));
            _text.Append("\nradius ").Append(Length(b.Radius));
            if (!double.IsInfinity(b.SoiRadius)) _text.Append("\nsphere of influence ").Append(Length(b.SoiRadius));
            if (b.Parent >= 0) _text.Append("\norbit ").Append(Length(b.Orbit.SemiMajorAxis)).Append(", period ").Append(Duration(b.Orbit.Period));
            if (craft.Exists) _text.Append("\nfrom you ").Append(Length(Vec3.Length(_global[craft.Parent] + craft.Position - _global[i])));
            return _text.ToString();
        }

        private string CraftText(double time, in MapCraft craft, TrajectoryPlot plot)
        {
            _text.Clear();
            _text.Append(craft.Callsign).Append("\naround ").Append(NameOf(craft.Parent)).Append("  ").Append(ModeName(craft.Mode));
            _text.Append("\nheight ").Append(Length(craft.Altitude));
            _text.Append("\nspeed ").Append(Speed(Vec3.Length(craft.Velocity)));
            if (plot.Count > 0 && plot.Parent == craft.Parent)
            {
                ref readonly TrajectorySegment s = ref plot.Segments[0];
                double radius = _frames[s.Body].Radius;
                _text.Append("\nPe ").Append(Length(s.Orbit.Pericenter - radius));
                if (s.Orbit.Eccentricity < 1.0) _text.Append("  Ap ").Append(Length(s.Orbit.Apocenter - radius));
            }
            return _text.ToString();
        }

        private string NameOf(int body) => _nameOf(_frames.IdOf(body));

        private static string ModeName(int mode) => mode == FlightMode.Flying ? "flying" : mode == FlightMode.Landed ? "landed" : "crashed";

        public static string Length(double meters)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            double a = Math.Abs(meters);
            if (a < 10000.0) return meters.ToString("F0", c) + " m";
            if (a < 1e7) return (meters / 1000.0).ToString("F1", c) + " km";
            if (a < 1e10) return (meters / 1000.0).ToString("N0", c) + " km";
            if (a < 1.5e10) return (meters / 1e9).ToString("F2", c) + " M km";
            return (meters / 1.495978707e11).ToString("F2", c) + " AU";
        }

        public static string Speed(double metersPerSecond)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return Math.Abs(metersPerSecond) < 10000.0 ? metersPerSecond.ToString("F1", c) + " m/s" : (metersPerSecond / 1000.0).ToString("F2", c) + " km/s";
        }

        public static string Duration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "-";
            CultureInfo c = CultureInfo.InvariantCulture;
            if (seconds < 100.0) return seconds.ToString("F1", c) + " s";
            long s = (long) seconds;
            long d = s / 86400, h = s / 3600 % 24, m = s / 60 % 60;
            s %= 60;
            if (d >= 365) return (seconds / 31557600.0).ToString("F1", c) + " y";
            if (d > 0) return d + "d " + h + "h " + m + "m";
            if (h > 0) return h + "h " + m + "m " + s + "s";
            return m + "m " + s + "s";
        }

        // ------------------------------------------------------------------ math

        private Vector3 ToMap(Vec3 global)
        {
            return CraftPresenter.ToVector((global - _focus) * _unit);
        }

        // Pixels, y up. The matrix once a frame: the trajectory projects hundreds of points.
        private bool TryScreen(Vector3 at, out Vector2 screen)
        {
            Vector4 clip = _viewProjection * new Vector4(at.x, at.y, at.z, 1f);
            if (clip.w <= 0f)
            {
                screen = default;
                return false;
            }
            screen = new Vector2((clip.x / clip.w * 0.5f + 0.5f) * _camera.pixelWidth, (clip.y / clip.w * 0.5f + 0.5f) * _camera.pixelHeight);
            return screen.x > -2000f && screen.x < _camera.pixelWidth + 2000f && screen.y > -2000f && screen.y < _camera.pixelHeight + 2000f;
        }

        private Vector2 Gui(Vector2 screen) => new Vector2(screen.x, _camera.pixelHeight - screen.y);

        private static Color Bright(Color tint)
        {
            float max = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
            Color c = max > 0.001f ? tint * (0.9f / max) : Color.gray;
            c = Color.Lerp(c, Color.white, 0.25f);
            c.a = 1f;
            return c;
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

        public void Dispose()
        {
            for (int i = 0; i < _bodyMaterials.Length; i++)
            {
                if (_bodyMaterials[i] != null) Object.Destroy(_bodyMaterials[i]);
                _orbits[i]?.Dispose();
            }
            foreach (MapLine line in _trajectory) line?.Dispose();
            if (_sphere != null) Object.Destroy(_sphere);
            if (_root != null) Object.Destroy(_root);
        }

        /// <summary>One polyline of the map: a mesh rewritten when the view moves, see the shader Orbital/MapLine.</summary>
        private sealed class MapLine : IDisposable
        {
            private readonly GameObject _object;
            private readonly Mesh _mesh;
            private readonly Material _material;
            private readonly Vector3[] _positions;
            private readonly Vector3[] _others;
            private static readonly int ColorId = Shader.PropertyToID("_Color");
            private static readonly int WidthId = Shader.PropertyToID("_WidthPx");

            public MapLine(string name, int points, Shader shader, int layer, Transform parent)
            {
                int segments = points - 1;
                _positions = new Vector3[segments * 4];
                _others = new Vector3[segments * 4];
                Vector2[] sides = new Vector2[segments * 4];
                int[] triangles = new int[segments * 6];
                for (int i = 0; i < segments; i++)
                {
                    int v = i * 4;
                    sides[v] = new Vector2(-1f, 1f);
                    sides[v + 1] = new Vector2(1f, 1f);
                    sides[v + 2] = new Vector2(-1f, -1f);
                    sides[v + 3] = new Vector2(1f, -1f);
                    int t = i * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 2;
                    triangles[t + 3] = v + 1;
                    triangles[t + 4] = v + 3;
                    triangles[t + 5] = v + 2;
                }
                _mesh = new Mesh {name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32};
                _mesh.MarkDynamic();
                _mesh.vertices = _positions;
                _mesh.SetUVs(0, sides);
                _mesh.SetUVs(1, _others);
                _mesh.triangles = triangles;
                _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2e9f); // the line is anywhere: never culled by the object's box
                _material = new Material(shader);
                _object = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                _object.layer = layer;
                _object.transform.SetParent(parent, false);
                _object.GetComponent<MeshFilter>().sharedMesh = _mesh;
                MeshRenderer renderer = _object.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                _object.SetActive(false);
            }

            public void SetVisible(bool visible)
            {
                if (_object.activeSelf != visible) _object.SetActive(visible);
            }

            public void Set(Vector3[] points, Color color, float width)
            {
                int segments = _positions.Length / 4;
                for (int i = 0; i < segments; i++)
                {
                    Vector3 a = points[i], b = points[i + 1];
                    int v = i * 4;
                    _positions[v] = a;
                    _positions[v + 1] = a;
                    _positions[v + 2] = b;
                    _positions[v + 3] = b;
                    _others[v] = b;
                    _others[v + 1] = b;
                    _others[v + 2] = a;
                    _others[v + 3] = a;
                }
                _mesh.vertices = _positions;
                _mesh.SetUVs(1, _others);
                _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2e9f);
                _material.SetColor(ColorId, color);
                _material.SetFloat(WidthId, width);
            }

            public void Dispose()
            {
                if (_mesh != null) Object.Destroy(_mesh);
                if (_material != null) Object.Destroy(_material);
            }
        }
    }
}
