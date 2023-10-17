namespace Core.Patterns.State
{
    public interface IStateMachine
    {
        State CurrentState { get; set; }
    }

    public abstract class State
    {
        public abstract void Update();
    }

    public abstract class State<T> : State where T : IStateMachine
    {
        public T Master;
        public State(T master)
        {
            this.Master = master;
        }
    }
}
