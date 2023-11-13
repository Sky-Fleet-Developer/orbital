using System;

namespace Orbital.Core.Serialization.Sqlite
{
    public class PrimaryKeyAttribute : Attribute
    {
    }

    public class ReferenceAttribute : Attribute
    {
        public string TableReference;
        public string RowReference;

        public ReferenceAttribute(string tableReference, string rowReference)
        {
            TableReference = tableReference;
            RowReference = rowReference;
        }
    }

    public class DataTypeAttribute : Attribute
    {
        public string Type;

        public DataTypeAttribute(string type)
        {
            Type = type;
        }
    }

    public class DataConverterAttribute : Attribute
    {
        public DataConverter DataConverter;

        public DataConverterAttribute(DataConverter dataConverter)
        {
            DataConverter = dataConverter;
        }
    }
}