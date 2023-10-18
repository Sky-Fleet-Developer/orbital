#if  UNITY_EDITOR
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class CreateRootState : WorldDataState, INestedStateUser<IMassSystem>
        {
            public CreateRootState(WorldDataEditor master) : base(master)
            {
            }

            public override void Update()
            {
                if (GUILayout.Button("Create root", GUILayout.Width(DefaultButtonSize)))
                {
                    Master._currentState = new SelectNewChildState(this, PossibleMass.Dual | PossibleMass.Single, Master);
                }
            }

            public override void OnSceneGui()
            {
            }

            public void NestedStateCallback(IMassSystem value, bool final)
            {
                if (value == null)
                {
                    Master._currentState = this;
                    return;
                }

                Master._tree.Root = value;
                Master.ApplyChanges(final);
                if (final)
                {
                    Master._currentState = new ExtendTreeState(Master);
                }
            }
        }
    }
}
#endif
