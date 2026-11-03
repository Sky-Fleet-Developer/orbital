using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Orbital.Core.Flight;
using Orbital.Core.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.Craft
{
    /// <summary>
    /// Other players' crafts in the flight view (stage 2c.7): a greybox like the own craft, placed in the floating-origin view by the
    /// difference of double positions; only crafts around the same body and closer than <see cref="VisibleDistance"/> are drawn, the rest
    /// are listed (<see cref="ListText"/>) with callsign and distance.
    /// </summary>
    public sealed class GhostPresenter : System.IDisposable
    {
        public const double VisibleDistance = 20000.0;

        private readonly CraftPresenter _host;
        private readonly Dictionary<string, Transform> _views = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> _wreckViews = new Dictionary<string, Transform>();
        private readonly List<string> _gone = new List<string>();
        private readonly StringBuilder _list = new StringBuilder(256);

        public GhostPresenter(CraftPresenter host)
        {
            _host = host;
        }

        /// <summary>One line per other craft: callsign, distance or body, mode.</summary>
        public string ListText => _list.ToString();

        public void Present(Vec3 ownShownPosition, string ownParent, IReadOnlyDictionary<string, CraftNetLink.Other> others)
        {
            _list.Clear();
            CultureInfo c = CultureInfo.InvariantCulture;
            foreach (KeyValuePair<string, CraftNetLink.Other> kv in others)
            {
                Ghost g = kv.Value.Ghost;
                if (!g.HasState) continue;
                bool sameBody = ownParent == null || kv.Value.ParentId == ownParent;
                double distance = sameBody ? Vec3.Length(g.ShownPosition - ownShownPosition) : double.PositiveInfinity;
                _list.Append(g.Callsign ?? g.CraftId).Append("  ");
                _list.Append(sameBody ? (distance >= 10000 ? (distance / 1000).ToString("F1", c) + " km" : distance.ToString("F0", c) + " m") : "@ " + kv.Value.ParentId);
                _list.Append(g.ServerMode == FlightMode.Crashed ? "  crashed" : g.ServerMode == FlightMode.Landed ? "  landed" : "").Append('\n');
                bool show = sameBody && distance < VisibleDistance;
                Transform view = ViewOf(kv.Key, show);
                if (view == null) continue;
                if (view.gameObject.activeSelf != show) view.gameObject.SetActive(show);
                if (!show) continue;
                view.localPosition = CraftPresenter.ToVector(g.ShownPosition - ownShownPosition);
                view.localRotation = CraftPresenter.ToQuaternion(g.State.Orientation);
            }
            _gone.Clear();
            foreach (string id in _views.Keys) if (!others.ContainsKey(id)) _gone.Add(id);
            foreach (string id in _gone)
            {
                if (_views[id] != null) Object.Destroy(_views[id].gameObject);
                _views.Remove(id);
            }
        }

        /// <summary>Wreck markers (stage 2c.7): a small red block on the ground of the same body, closer than <see cref="VisibleDistance"/>.</summary>
        public void PresentWrecks(Vec3 ownShownPosition, string ownParent, IReadOnlyDictionary<string, WreckDto> wrecks, in SurfaceData surface, double time)
        {
            foreach (KeyValuePair<string, WreckDto> kv in wrecks)
            {
                WreckDto w = kv.Value;
                Vec3 at = Surface.Point(surface, w.Latitude, w.Longitude, time);
                bool show = (ownParent == null || w.ParentId == ownParent) && Vec3.Length(at - ownShownPosition) < VisibleDistance;
                if (!_wreckViews.TryGetValue(kv.Key, out Transform t))
                {
                    if (!show) continue;
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Wreck " + (w.Callsign ?? w.Id);
                    Object.Destroy(go.GetComponent<Collider>());
                    Material m = _host.Materials.Get("wreck");
                    Color red = new Color(0.8f, 0.1f, 0.05f);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", red); // URP Lit
                    else m.color = red;
                    go.GetComponent<Renderer>().sharedMaterial = m;
                    go.transform.SetParent(_host.Root, false);
                    go.transform.localScale = new Vector3(3f, 1.5f, 3f);
                    go.layer = _host.Layer;
                    _wreckViews[kv.Key] = t = go.transform;
                }
                if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
                if (!show) continue;
                t.localPosition = CraftPresenter.ToVector(at - ownShownPosition);
                Vec3 up = Vec3.Normalize(at);
                t.localRotation = Quaternion.FromToRotation(Vector3.up, CraftPresenter.ToVector(up));
            }
        }

        private Transform ViewOf(string id, bool create)
        {
            if (_views.TryGetValue(id, out Transform t)) return t;
            if (!create) return null;
            GameObject go = GreyboxVehicleFactory.Build(_host.CgHeight, _host.Materials.Get);
            go.name = "Other " + id;
            foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);
            Transform flame = go.transform.Find(GreyboxVehicleFactory.FlameName);
            if (flame != null) flame.gameObject.SetActive(false);
            go.transform.SetParent(_host.Root, false);
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = _host.Layer;
            _views[id] = go.transform;
            return go.transform;
        }

        public void Dispose()
        {
            foreach (Transform t in _views.Values) if (t != null) Object.Destroy(t.gameObject);
            _views.Clear();
            foreach (Transform t in _wreckViews.Values) if (t != null) Object.Destroy(t.gameObject);
            _wreckViews.Clear();
        }
    }
}
