using System;
using System.Collections;
using System.Collections.Generic;
using Ara3D;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
[ExecuteAlways]
public class TrajectoryTest : MonoBehaviour
{
    public StaticBody body;
    private IStaticBody _body;
    private IStaticBody _parent;
    private World _world;

    public StaticTrajectory Trajectory;
    public DVector3 pos;
    public DVector3 vel;
    public double testT;
    public double testD;
    public double t;
    public double epoch;
    private const float scale = 4.456328E-09F;
    void Refresh()
    {
        _body = body;
        _parent = GetComponentInParent<IStaticBody>();
        _world = GetComponentInParent<World>();
        _world.Load();
        Trajectory = new StaticTrajectory(_parent.MassSystem);
        Trajectory.Calculate(pos, vel, epoch);
    }

    private void OnValidate()
    {
        Trajectory.Calculate(pos, vel, epoch);
        Vector3 scaledPos = _parent.LocalPosition * scale;
        t = MassUtility.GetClosestPointTime(Trajectory, _body.Trajectory, 0, scaledPos, scale);
    }

    void Update()
    {
        if(!enabled) return;
        if (_parent == null)
        {
            Refresh();
        }
        
    }

    private void OnDrawGizmosSelected()
    {
        if(!enabled) return;
        Handles.color = Color.green * 0.7f;
        Vector3 scaledPos = _parent.LocalPosition * scale;
        Debug.DrawLine(scaledPos, scaledPos + (Vector3)Trajectory.GetPositionAtT(t - Trajectory.Epoch) * scale, Color.cyan);
        Debug.DrawLine(scaledPos, scaledPos + (Vector3)_body.Trajectory.GetPositionAtT(t - _body.Trajectory.Epoch) * scale, Color.cyan);
        var a = (Vector3) Trajectory.GetPositionAtT(testT - Trajectory.Epoch) * scale;
        var b = (Vector3) _body.Trajectory.GetPositionAtT(testT - _body.Trajectory.Epoch) * scale;
        Debug.DrawLine(scaledPos, scaledPos + a, Color.magenta);
        Debug.DrawLine(scaledPos, scaledPos + b, Color.magenta);
        testD = (a - b).magnitude;
        Trajectory.DrawGizmos(_parent.LocalPosition);
    }
}
