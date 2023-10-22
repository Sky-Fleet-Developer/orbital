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
            private DoubleSystemTrajectorySettings _trajectorySettingsDest;
            private DoubleSystemTrajectorySettings _trajectorySettingsSource;
            private DoubleSystemTrajectorySettings _original;
            public SetupDoubleSettingsState(DoubleSystemTrajectorySettings original, INestedStateUser<DoubleSystemTrajectorySettings?> lastState, WorldDataEditor master) : base(master)
            {
                _lastState = lastState;
                _trajectorySettingsDest = original;
                _trajectorySettingsSource = original;
                _original = original;
            }

            public override void Update()
            {
                GUILayout.Label("Setup:");
                EditorGUI.BeginChangeCheck();

                double abK = Math.Pow(_trajectorySettingsDest.aMass / _trajectorySettingsDest.bMass, 1.0 / 3.0);
                double bPericenter = _trajectorySettingsDest.aPericenterRadius * abK;
                double aMajor = StaticTrajectory.GetSemiMajorAxis(_trajectorySettingsDest.eccentricity, _trajectorySettingsDest.aPericenterRadius);
                double bMajor = StaticTrajectory.GetSemiMajorAxis(_trajectorySettingsDest.eccentricity, bPericenter);

                _trajectorySettingsDest.aMass = EditorGUILayout.FloatField("A body mass", _trajectorySettingsSource.aMass);
                _trajectorySettingsDest.bMass = EditorGUILayout.FloatField("B body mass", _trajectorySettingsSource.bMass);
                _trajectorySettingsDest.eccentricity = EditorGUILayout.FloatField("Eccentricity", _trajectorySettingsSource.eccentricity);
                _trajectorySettingsDest.aPericenterRadius = EditorGUILayout.FloatField("A body pericenter", _trajectorySettingsSource.aPericenterRadius);
                
                
                GUILayout.Box($"B body pericenter: {bPericenter :e2}");
                _trajectorySettingsDest.argumentOfPeriapsis = EditorGUILayout.FloatField("Argument Of Periapsis", _trajectorySettingsSource.argumentOfPeriapsis);
                _trajectorySettingsDest.longitudeAscendingNode = EditorGUILayout.FloatField("Longitude Ascending Node", _trajectorySettingsSource.longitudeAscendingNode);
                _trajectorySettingsDest.inclination = EditorGUILayout.FloatField("Inclination", _trajectorySettingsSource.inclination);
                _trajectorySettingsDest.timeShift = EditorGUILayout.FloatField("Time", _trajectorySettingsSource.timeShift);
                

                GUILayout.Box($"Semi major axis (a): {aMajor :e2}");                
                GUILayout.Box($"Semi major axis (b): {bMajor :e2}");
                
                if (EditorGUI.EndChangeCheck())
                {
                    _trajectorySettingsSource = _trajectorySettingsDest;
                    _lastState.NestedStateCallback(_trajectorySettingsSource, false);
                }
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ok"))
                {
                    _lastState.NestedStateCallback(_trajectorySettingsSource);
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
