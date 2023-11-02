using Ara3D;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class TrajectoryTest : MonoBehaviour
{
    public StaticBody body;
    private IStaticBody _body;
    private IStaticBody _parent;
    private World _world;

    public bool needSample;
    public StaticOrbit Orbit;
    public DVector3 pos;
    public DVector3 vel;
    public double testT;
    public double testD;
    public double t;
    public double epoch;
    public double testEpoch;
    private const float scale = 4.456328E-09F;

    void Refresh()
    {
        _body = body;
        _parent = GetComponentInParent<IStaticBody>();
        _world = GetComponentInParent<World>();
        _world.Load();
        Orbit = new StaticOrbit(_parent.MassSystem);
        if (needSample)
        {
            _body.Orbit.GetOrbitalStateVectorsAtOrbitTime(0, out pos, out vel);
        }

        Orbit.Calculate(pos, vel, epoch);
    }

    private void OnValidate()
    {
        Refresh();
        //Vector3 scaledPos = _parent.LocalPosition * scale;
        // double gravityRadius = MassUtility.GetGravityRadius(_body.GravParameter);
        //t = MassUtility.GetClosestPointTimeForDistance(Orbit, _body.Orbit, gravityRadius, 0, out double distance);
    }

    void Update()
    {
        if (!enabled) return;
        if (_parent == null)
        {
            Refresh();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!enabled) return;
        Handles.color = Color.green * 0.7f;
        Vector3 scaledPos = _parent.LocalPosition * scale;
        var a = (Vector3) Orbit.GetPositionAtT(t) * scale;
        //var b = (Vector3) _body.Orbit.GetPositionAtT(t - _body.Orbit.Epoch) * scale;
        Debug.DrawLine(scaledPos, scaledPos + a, Color.cyan);
        //Debug.DrawLine(scaledPos, scaledPos + b, Color.cyan);
        var at = (Vector3) Orbit.GetPositionAtT(testEpoch) * scale;
        //var bt = (Vector3) _body.Orbit.GetPositionAtT(testEpoch - _body.Orbit.Epoch) * scale;
        Debug.DrawLine(scaledPos, scaledPos + at, Color.magenta);
        Debug.DrawRay(pos * scale, vel * 0.005f, Color.red);
        //Debug.DrawLine(scaledPos, scaledPos + bt, Color.magenta);
        //var d = Quaternion.LookRotation(a - b, Vector3.up) * Quaternion.Euler(90, 0, 0);
        //Handles.CircleHandleCap(-1, scaledPos + b, d, (float)MassUtility.GetGravityRadius(_body.GravParameter) * scale, EventType.Repaint);
        Handles.color = Color.magenta * 0.7f;
        //var dt = Quaternion.LookRotation(at - bt, Vector3.up) * Quaternion.Euler(90, 0, 0);
        //Handles.CircleHandleCap(-1, scaledPos + bt, dt, (float)MassUtility.GetGravityRadius(_body.GravParameter) * scale, EventType.Repaint);
        //testD = (a - b).magnitude / scale;
        Orbit.DrawGizmos(_parent.LocalPosition + DVector3.up * 10000);
    }
}