using Core.Patterns.State;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private abstract class WorldDataState : State<WorldDataEditor>
        {
            protected WorldDataState(WorldDataEditor master) : base(master)
            {
            }

            public abstract void OnSceneGui();
        }
    }
}