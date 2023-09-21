namespace Core.Patterns.State
{
    public interface IStateMaster
    {
        State CurrentState { get; set; }
    }

    public abstract class State
    {
        public abstract void Update();
    }

    public abstract class State<T> : State where T : IStateMaster
    {
        public T Master;
        public State(T master)
        {
            this.Master = master;
        }
    }
}
