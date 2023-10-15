namespace Orbital.Core.Handles
{
    public interface IFixedUpdateHandler : IOrderHolder
    {
        public void FixedUpdate();
    }
}