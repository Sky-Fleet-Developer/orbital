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
        private class SetupDoubleSettingsState : WorldDataState
        {
            private INestedStateUser<DoubleSystemSettings?> _lastState;
            private DoubleSystemSettings _settingsA;
            private DoubleSystemSettings _settingsB;
            private DoubleSystemSettings _original;
            public SetupDoubleSettingsState(DoubleSystemSettings original, INestedStateUser<DoubleSystemSettings?> lastState, WorldDataEditor master) : base(master)
            {
                _lastState = lastState;
                _settingsA = original;
                _settingsB = original;
                _original = original;
            }

            public override void Update()
            {
                GUILayout.Label("Setup:");
                EditorGUI.BeginChangeCheck();
                
                double aMajor = RelativeTrajectory.GetSemiMajorAxis(_settingsA.bMass, _settingsA.period, OrbitCalculationService.G);
                double aEccentricity = RelativeTrajectory.GetEccentricity(_settingsA.aPericenterRadius, aMajor);
                double bMajor = RelativeTrajectory.GetSemiMajorAxis(_settingsA.aMass, _settingsA.period, OrbitCalculationService.G);
                double bPericenter = _settingsA.aPericenterRadius * Math.Pow(_settingsA.aMass / _settingsA.bMass, 1.0 / 3.0);
                double bEccentricity = RelativeTrajectory.GetEccentricity(bPericenter, bMajor);

                _settingsA.aMass = EditorGUILayout.FloatField("A body mass", _settingsB.aMass);
                _settingsA.bMass = EditorGUILayout.FloatField("B body mass", _settingsB.bMass);
                _settingsA.period = EditorGUILayout.FloatField("Period", _settingsB.period);
                _settingsA.aPericenterRadius = EditorGUILayout.FloatField("A body pericenter", _settingsB.aPericenterRadius);
                
                
                GUILayout.Box($"B body pericenter: {bPericenter :e2}");
                _settingsA.latitudeShift = EditorGUILayout.FloatField("Latitude", _settingsB.latitudeShift);
                _settingsA.longitudeShift = EditorGUILayout.FloatField("Longitude", _settingsB.longitudeShift);
                _settingsA.inclination = EditorGUILayout.FloatField("Inclination", _settingsB.inclination);
                _settingsA.timeShift = EditorGUILayout.FloatField("Time", _settingsB.timeShift);
                

                
                GUILayout.Box($"Eccentricity (a): {aEccentricity}");
                GUILayout.Box($"Semi major axis (a): {aMajor :e2}");                
                GUILayout.Box($"Eccentricity (b): {bEccentricity}");
                GUILayout.Box($"Semi major axis (b): {bMajor :e2}");
                
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