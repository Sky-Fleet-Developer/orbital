using System;
using Orbital.Model.Services;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class SetupCelestialSettingsState : WorldDataState
        {
            private INestedStateUser<CelestialSettings?> _lastState;
            private CelestialSettings _settingsA;
            private CelestialSettings _settingsB;
            private CelestialSettings _original;

            public SetupCelestialSettingsState(CelestialSettings original, INestedStateUser<CelestialSettings?> lastState,
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
                
                _settingsA.pericenterRadius = EditorGUILayout.FloatField("Pericenter", _settingsB.pericenterRadius);
                _settingsA.pericenterSpeed = EditorGUILayout.FloatField("Speed", _settingsB.pericenterSpeed);
                _settingsA.latitudeShift = EditorGUILayout.FloatField("Latitude", _settingsB.latitudeShift);
                _settingsA.longitudeShift = EditorGUILayout.FloatField("Longitude", _settingsB.longitudeShift);
                _settingsA.inclination = EditorGUILayout.FloatField("Inclination", _settingsB.inclination);
                _settingsA.timeShift = EditorGUILayout.FloatField("Time", _settingsB.timeShift);
                double m = Master._currentParent.Mass;
                double e = RelativeTrajectory.GetEccentricity(_settingsA.pericenterSpeed, _settingsA.pericenterRadius,
                    m, OrbitCalculationService.G);
                double a = RelativeTrajectory.GetSemiMajorAxis(e, _settingsA.pericenterRadius);
                double t = RelativeTrajectory.GetPeriod(a, OrbitCalculationService.G, m);
                GUILayout.Box($"Eccentricity: {e}");
                GUILayout.Box($"Semi major axis: {a}");
                if (double.IsNaN(t))
                {
                    GUILayout.Box($"Period: NaN");
                }
                else
                {
                    var ts = TimeSpan.FromSeconds((long) t);
                    GUILayout.Box($"Period: {ts.Days} d, {ts.Hours} h, {ts.Minutes} m");
                }

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