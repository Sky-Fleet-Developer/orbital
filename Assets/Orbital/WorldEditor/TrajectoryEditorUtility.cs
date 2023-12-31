using System;
using System.Collections.Generic;
using Ara3D;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
    #if UNITY_EDITOR
    public static class TrajectoryEditorUtility
    {
        public static void DrawTrajectory(StaticOrbit orbit, double time, float scale, bool drawSphere, DVector3 inputOrigin, out DVector3 outputOrigin)
        {
            var sample = orbit.GetPositionAtT(time);
            outputOrigin = sample + inputOrigin;
            Vector3 sOutput = (Vector3) (outputOrigin) * scale;
            //Vector3 sPericenter = (Vector3) (orbit.GetSample(-orbit.TimeShift * orbit.period, true, false).position + inputOrigin) * scale;
            Vector3 sInput = (Vector3) (inputOrigin) * scale;
            Handles.matrix = orbit.GetMatrixForTPreview(scale, sInput, 0);
            Handles.CircleHandleCap(-1, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
            Handles.matrix = Matrix4x4.identity;
            //Vector3 velocity = sample.velocity * scale;
            Handles.DrawDottedLine(sOutput, sInput, 10);
            Handles.color = Color.green;
            //Handles.DrawDottedLine(sPericenter, sInput, 6);
            //Handles.DrawLine(sOutput, sOutput + velocity * 200);
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
        
        private static void DrawTrajectoriesRecursively(IMassSystem massSystem, Dictionary<IMassSystem, StaticOrbit> trajectories, DVector3 origin, double time, float scale)
        {
            if(massSystem == null) return;
            if(!trajectories.TryGetValue(massSystem, out StaticOrbit trajectory)) return;
            
            DrawTrajectory(trajectory, time, scale, massSystem is CelestialBody, origin, out DVector3 output);
            foreach (IMassSystem child in massSystem.GetContent())
            {
                DrawTrajectoriesRecursively(child, trajectories, output, time, scale);
            }
        }
        
        public static Matrix4x4 GetMatrixForTPreview(this StaticOrbit orbit, float scale, Vector3 position, double time)
        {
            Quaternion handleRotationCorrection = Quaternion.Euler(-90, 0, 0);
            Matrix4x4 rotationMatrix = orbit.RotationMatrix;
            Matrix4x4 figureMatrix = Matrix4x4.TRS(new Vector3(0, 0, (float)((orbit.Pericenter - orbit.SemiMajorAxis) * scale)), handleRotationCorrection, new Vector3((float)orbit.SemiMinorAxis, (float)orbit.SemiMajorAxis, 0) * scale);
            Matrix4x4 worldMatrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            return worldMatrix * rotationMatrix * figureMatrix;
        }
    }
    #endif
}
