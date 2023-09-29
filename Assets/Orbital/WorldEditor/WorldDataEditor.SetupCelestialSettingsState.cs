using Orbital.Model.Components;
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
                _settingsA.mass = EditorGUILayout.DelayedFloatField("Mass", _settingsB.mass);
                _settingsA.pericenterRadius = EditorGUILayout.DelayedFloatField("Pericenter", _settingsB.pericenterRadius);
                _settingsA.pericenterSpeed = EditorGUILayout.DelayedFloatField("Speed", _settingsB.pericenterSpeed);
                _settingsA.latitudeShift = EditorGUILayout.DelayedFloatField("Latitude", _settingsB.latitudeShift);
                _settingsA.longitudeShift = EditorGUILayout.DelayedFloatField("Longitude", _settingsB.longitudeShift);
                _settingsA.periodShift = EditorGUILayout.DelayedFloatField("Period", _settingsB.periodShift);
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