namespace Orbital.Core.Bodies
{
    /// <summary>Gives the model its per-body data (radius, rotation...) by the name of the scene object. Implemented by <see cref="BodyTable"/>.</summary>
    public interface IBodyDataSource
    {
        bool TryGet(string objectName, out BodyRecord record);
    }
}
