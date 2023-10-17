namespace Orbital.Core.Handles
{
    public interface ILateUpdateHandler : IOrderHolder
    {
        public void LateUpdate();
    }
}
