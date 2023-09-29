using Orbital.Model.TrajectorySystem;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class CreateRootState : WorldDataState, INestedStateUser<IMass>
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

            public void NestedStateCallback(IMass value)
            {
                if (value == null)
                {
                    Master._currentState = this;
                    return;
                }

                Master._container.Root = value;
                Master.ApplyChanges();
                Master._currentState = new ExtendTreeState(Master);
            }
        }
    }
}