namespace Orbital.Core.Handles
{
    public interface IUpdateByFrequencyHandler : IOrderHolder
    {
        UpdateFrequency Frequency { get; }
        void Update();
    }

    public enum UpdateFrequency
    {
        Every10Frame = 0,
        Every100Frame = 1,
        Every1000Frame = 2
    }
}
