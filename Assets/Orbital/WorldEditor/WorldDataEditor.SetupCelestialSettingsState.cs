#if  UNITY_EDITOR
using System;
using Orbital.Core.TrajectorySystem;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class SetupCelestialSettingsState : WorldDataState
        {
            private INestedStateUser<TrajectorySettings?> _lastState;
            private TrajectorySettings _settingsA;
            private TrajectorySettings _settingsB;
            private TrajectorySettings _original;

            public SetupCelestialSettingsState(TrajectorySettings original, INestedStateUser<TrajectorySettings?> lastState,
                WorldDataEditor master) : base(master)
            {
                _settingsA = original;
                _settingsB = original;
                _original = original;
                _lastState = lastState;
            }

            public override void Update()
            {
                GUILayout.Label("Setup:");

                EditorGUI.BeginChangeCheck();
                if (Master._currentEdit is CelestialBody)
                {
                    _settingsA.mass = EditorGUILayout.FloatField("Mass", _settingsB.mass);
                }
                
                _settingsA.semiMajorAxis = EditorGUILayout.FloatField("Semi Major Axis", _settingsB.semiMajorAxis);
                _settingsA.eccentricity = EditorGUILayout.FloatField("Eccentricity", _settingsB.eccentricity);
                _settingsA.argumentOfPeriapsis = EditorGUILayout.FloatField("Argument Of Periapsis", _settingsB.argumentOfPeriapsis);
                _settingsA.longitudeAscendingNode = EditorGUILayout.FloatField("Longitude Ascending Node", _settingsB.longitudeAscendingNode);
                _settingsA.inclination = EditorGUILayout.FloatField("Inclination", _settingsB.inclination);
                _settingsA.timeShift = EditorGUILayout.FloatField("Time", _settingsB.timeShift);
                /*double m = Master._currentParent.Mass;
                double e = StaticTrajectory.GetEccentricity(_settingsA.eccentricity, _settingsA.semiMajorAxis,
                    m, MassUtility.G);
                double a = StaticTrajectory.GetSemiMajorAxis(e, _settingsA.semiMajorAxis);
                double t = StaticTrajectory.GetPeriod(a, MassUtility.G, m);
                GUILayout.Box($"eccentricity: {e}");
                GUILayout.Box($"Semi major axis: {a}");
                if (double.IsNaN(t))
                {
                    GUILayout.Box($"period: NaN");
                }
                else
                {
                    var ts = TimeSpan.FromSeconds((long) t);
                    GUILayout.Box($"period: {ts.Days} d, {ts.Hours} h, {ts.Minutes} m");
                }*/

                if (EditorGUI.EndChangeCheck())
                {
                    _settingsB = _settingsA;
                    _lastState.NestedStateCallback(_settingsB, false);
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ok"))
                {
                    _lastState.NestedStateCallback(_settingsB);
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
