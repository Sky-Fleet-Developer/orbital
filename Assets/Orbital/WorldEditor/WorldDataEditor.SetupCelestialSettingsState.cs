using Orbital.Model.Components;
using Orbital.Model.Services;
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

            public SetupCelestialSettingsState(CelestialSettings original, INestedStateUser<CelestialSettings?> lastState,
                WorldDataEditor master) : base(master)
            {
                _settingsA = original;
                _settingsB = original;
                _lastState = lastState;
            }
            public SetupCelestialSettingsState(INestedStateUser<CelestialSettings?> lastState,
                WorldDataEditor master) : base(master)
            {
                _lastState = lastState;
            }

            public override void Update()
            {
                GUILayout.Label("Setup:");

                EditorGUI.BeginChangeCheck();
                _settingsA.mass = EditorGUILayout.FloatField("Mass", _settingsB.mass);
                _settingsA.pericenterRadius = EditorGUILayout.FloatField("Pericenter", _settingsB.pericenterRadius);
                _settingsA.pericenterSpeed = EditorGUILayout.FloatField("Speed", _settingsB.pericenterSpeed);
                _settingsA.latitudeShift = EditorGUILayout.FloatField("Latitude", _settingsB.latitudeShift);
                _settingsA.longitudeShift = EditorGUILayout.FloatField("Longitude", _settingsB.longitudeShift);
                _settingsA.inclination = EditorGUILayout.FloatField("Inclination", _settingsB.inclination);
                _settingsA.periodShift = EditorGUILayout.FloatField("Period", _settingsB.periodShift);
                double e = RelativeTrajectory.GetEccentricity(_settingsA.pericenterSpeed, _settingsA.pericenterRadius,
                    Master._currentParent.Mass - _settingsA.mass, OrbitCalculationService.G);
                double a = RelativeTrajectory.GetSemiMajorAxis(e, _settingsA.pericenterRadius);
                GUILayout.Box($"Eccentricity: {e}");
                GUILayout.Box($"Semi major axis: {a}");
                
                if (EditorGUI.EndChangeCheck())
                {
                    _settingsB = _settingsA;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ok"))
                {
                    _lastState.NestedStateCallback(_settingsB);
                }

                if (GUILayout.Button("Cancel"))
                {
                    _lastState.NestedStateCallback(null);
                }

                GUILayout.EndHorizontal();
            }

            public override void OnSceneGui()
            {
            }
        }
    }
}