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
    private IStaticBody _body;
    private World _world;

    public StaticTrajectory Trajectory;
    public DVector3 pos;
    public DVector3 vel;
    public double gravityRadius;
    public double trueAnomalyAtRadius;
    private const float scale = 4.456328E-09F;
    void Refresh()
    {
        _body = GetComponentInParent<IStaticBody>();
        _world = GetComponentInParent<World>();
        _world.Load();
        Trajectory = new StaticTrajectory(_body.MassSystem);
        Trajectory.Calculate(pos, vel);
    }

    private void OnValidate()
    {
        Trajectory.Calculate(pos, vel);
    }

    void Update()
    {
        if(!enabled) return;
        if (_body == null)
        {
            Refresh();
        }
        gravityRadius = MassUtility.GetGravityRadius(_body.GravParameter);
        trueAnomalyAtRadius = Trajectory.TrueAnomalyAtRadius(gravityRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if(!enabled) return;
        Handles.color = Color.green * 0.7f;
        Vector3 scaledPos = _body.LocalPosition * scale;
        Handles.CircleHandleCap(0, scaledPos, Quaternion.Euler(90, 0, 0), (float)gravityRadius * scale, EventType.Repaint);        
        Debug.DrawLine(scaledPos, scaledPos + (Vector3)Trajectory.GetPositionFromTrueAnomaly(trueAnomalyAtRadius) * scale, Color.cyan);
        Trajectory.DrawGizmos(_body.LocalPosition);
    }
}
