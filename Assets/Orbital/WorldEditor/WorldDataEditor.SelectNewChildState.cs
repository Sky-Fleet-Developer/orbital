#if  UNITY_EDITOR
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class SelectNewChildState : WorldDataState
        {
            private INestedStateUser<IMassSystem> _lastState;
            private PossibleMass _massMask;

            public SelectNewChildState(INestedStateUser<IMassSystem> lastState, PossibleMass mask, WorldDataEditor master) : base(master)
            {
                _lastState = lastState;
                _massMask = mask;
            }

            public override void Update()
            {
                if (_massMask.HasFlag(PossibleMass.Single) && GUILayout.Button("Planet with satellites", GUILayout.Width(DefaultButtonSize)))
                {
                    _lastState.NestedStateCallback(new SingleCenterBranch());
                }

                if (_massMask.HasFlag(PossibleMass.Dual) && GUILayout.Button("Dual system", GUILayout.Width(DefaultButtonSize)))
                {
                    _lastState.NestedStateCallback(new DoubleSystemBranch());
                }

                if (_massMask.HasFlag(PossibleMass.Celestial) && GUILayout.Button("Celestial", GUILayout.Width(DefaultButtonSize)))
                {
                    _lastState.NestedStateCallback(new CelestialBody());
                }
                
                if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                {
                    _lastState.NestedStateCallback(null);
                }
            }

            public override void OnSceneGui()
            {
            }
        }
    }
}
#endif
