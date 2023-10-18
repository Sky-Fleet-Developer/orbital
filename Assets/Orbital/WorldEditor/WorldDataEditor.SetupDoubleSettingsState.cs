#if  UNITY_EDITOR
using System;
using Orbital.Core.TrajectorySystem;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class SetupDoubleSettingsState : WorldDataState
        {
            private INestedStateUser<DoubleSystemTrajectorySettings?> _lastState;
            private DoubleSystemTrajectorySettings _trajectorySettingsA;
            private DoubleSystemTrajectorySettings _trajectorySettingsB;
            private DoubleSystemTrajectorySettings _original;
            public SetupDoubleSettingsState(DoubleSystemTrajectorySettings original, INestedStateUser<DoubleSystemTrajectorySettings?> lastState, WorldDataEditor master) : base(master)
            {
                _lastState = lastState;
                _trajectorySettingsA = original;
                _trajectorySettingsB = original;
                _original = original;
            }

            public override void Update()
            {
                GUILayout.Label("Setup:");
                EditorGUI.BeginChangeCheck();
                
                double aMajor = RelativeTrajectory.GetSemiMajorAxis(_trajectorySettingsA.bMass, _trajectorySettingsA.period, MassUtility.G);
                double aEccentricity = RelativeTrajectory.GetEccentricity(_trajectorySettingsA.aPericenterRadius, aMajor);
                double bMajor = RelativeTrajectory.GetSemiMajorAxis(_trajectorySettingsA.aMass, _trajectorySettingsA.period, MassUtility.G);
                double bPericenter = _trajectorySettingsA.aPericenterRadius * Math.Pow(_trajectorySettingsA.aMass / _trajectorySettingsA.bMass, 1.0 / 3.0);
                double bEccentricity = RelativeTrajectory.GetEccentricity(bPericenter, bMajor);

                _trajectorySettingsA.aMass = EditorGUILayout.FloatField("A body mass", _trajectorySettingsB.aMass);
                _trajectorySettingsA.bMass = EditorGUILayout.FloatField("B body mass", _trajectorySettingsB.bMass);
                _trajectorySettingsA.period = EditorGUILayout.FloatField("Period", _trajectorySettingsB.period);
                _trajectorySettingsA.aPericenterRadius = EditorGUILayout.FloatField("A body pericenter", _trajectorySettingsB.aPericenterRadius);
                
                
                GUILayout.Box($"B body pericenter: {bPericenter :e2}");
                _trajectorySettingsA.latitudeShift = EditorGUILayout.FloatField("Latitude", _trajectorySettingsB.latitudeShift);
                _trajectorySettingsA.longitudeShift = EditorGUILayout.FloatField("Longitude", _trajectorySettingsB.longitudeShift);
                _trajectorySettingsA.inclination = EditorGUILayout.FloatField("Inclination", _trajectorySettingsB.inclination);
                _trajectorySettingsA.timeShift = EditorGUILayout.FloatField("Time", _trajectorySettingsB.timeShift);
                

                
                GUILayout.Box($"Eccentricity (a): {aEccentricity}");
                GUILayout.Box($"Semi major axis (a): {aMajor :e2}");                
                GUILayout.Box($"Eccentricity (b): {bEccentricity}");
                GUILayout.Box($"Semi major axis (b): {bMajor :e2}");
                
                if (EditorGUI.EndChangeCheck())
                {
                    _trajectorySettingsB = _trajectorySettingsA;
                    _lastState.NestedStateCallback(_trajectorySettingsB, false);
                }
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ok"))
                {
                    _lastState.NestedStateCallback(_trajectorySettingsB);
                }

                if (GUILayout.Button("Cancel"))
                {
                    _lastState.NestedStateCallback(_original);
                }

                GUILayout.EndHorizontal();
            }

            public override void OnSceneGui()
            {
            }
        }
    }
}
#endif
