namespace Orbital.Core.Serialization.Sqlite
{
    public abstract class DataConverter
    {
        public abstract string Convert(object o);
    }

    /*public class VectorConverter : DataConverter
    {
        public override string Convert(object o)
        {
            
        }
    }*/
}