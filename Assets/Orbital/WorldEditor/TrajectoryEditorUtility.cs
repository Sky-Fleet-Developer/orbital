using System.Collections.Generic;
using Ara3D;
using Orbital.Model;
using Orbital.Model.TrajectorySystem;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
    #if UNITY_EDITOR
    public static class TrajectoryEditorUtility
    {
        public static void DrawTrajectory(RelativeTrajectory trajectory, double time, float scale, bool drawSphere, DVector3 inputOrigin, out DVector3 outputOrigin)
        {
            DVector3 localPosition = trajectory.GetPosition(time);
            outputOrigin = localPosition + inputOrigin;
            Vector3 sOutput = (Vector3) (outputOrigin) * scale;
            Vector3 sInput = (Vector3) (inputOrigin) * scale;
            Handles.matrix = trajectory.GetMatrixForTPreview(scale, sInput, 0);
            Handles.CircleHandleCap(-1, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
            Handles.matrix = Matrix4x4.identity;
            Vector3 velocity = trajectory.GetVelocity(time) * scale;
            Handles.DrawDottedLine(sOutput, sInput, 10);
            Handles.color = Color.green;
            Handles.DrawLine(sOutput, sOutput + velocity * 200);
            Handles.color = Color.white;
            if(drawSphere) Handles.SphereHandleCap(-1, sOutput, Quaternion.identity, 0.1f * HandleUtility.GetHandleSize(sOutput), EventType.Repaint);
        }

        public static void DrawTrajectories(this TreeContainer container, double time, float scale)
        {
            if(container.Root == null) return;
            
            foreach (IMassSystem mass in container.Root.GetContent())
            {
                if(mass == null) continue;
                DrawTrajectoriesRecursively(mass, container._trajectories, DVector3.Zero, time, scale);
            }
        }
        
        private static void DrawTrajectoriesRecursively(IMassSystem massSystem, Dictionary<IMassSystem, RelativeTrajectory> trajectories, DVector3 origin, double time, float scale)
        {
            if(massSystem == null) return;
            if(!trajectories.TryGetValue(massSystem, out RelativeTrajectory trajectory)) return;
            
            DrawTrajectory(trajectory, time, scale, massSystem is CelestialBody, origin, out DVector3 output);
            foreach (IMassSystem child in massSystem.GetContent())
            {
                DrawTrajectoriesRecursively(child, trajectories, output, time, scale);
            }
        }
        
        public static Matrix4x4 GetMatrixForTPreview(this RelativeTrajectory trajectory, float scale, Vector3 position, double time)
        {
            Quaternion handleRotationCorrection = Quaternion.Euler(-90, 0, 0);
            Quaternion trajectoryRotation = Quaternion.Euler(Mathf.Rad2Deg * (float) trajectory.LatitudeShift, Mathf.Rad2Deg * (float) trajectory.LongitudeShift, Mathf.Rad2Deg * (float) trajectory.Inclination);
           
            Matrix4x4 figureMatrix = Matrix4x4.TRS(new Vector3(0, 0, (float)((trajectory.PericenterRadius - trajectory.SemiMajorAxis) * scale)), handleRotationCorrection, new Vector3((float)trajectory.SemiMinorAxis, (float)trajectory.SemiMajorAxis, 0) * scale);
            Matrix4x4 worldMatrix = Matrix4x4.TRS(position, trajectoryRotation, Vector3.one);
            return worldMatrix * figureMatrix;
        }
    }
    #endif
}
