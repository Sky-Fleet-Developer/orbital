using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Orbital.Core.Serialization.Sqlite
{
    
    [Serializable]
    public class Model
    {
        public string tableName;
        public Member[] members;
        public ForeignKey[] foreignKeys;
        public string modelType;
        private Type _realType;
        
        public Model(Type modelType, string tableName)
        {
            this.tableName = tableName;
            this.modelType = modelType.AssemblyQualifiedName;
            _realType = modelType;
            List<Member> membersList = new List<Member>();
            List<ForeignKey> foreignKeysList = new List<ForeignKey>();
            PropertyInfo[] properties = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            
            for (int index = 0; index < properties.Length; index++)
            {
                PropertyInfo propertyInfo = properties[index];
                if (propertyInfo.PropertyType.IsPrimitive || propertyInfo.PropertyType.IsValueType || propertyInfo.PropertyType == typeof(string))
                {
                    membersList.Add(new Member(propertyInfo));
                }
                else
                {
                    ReferenceAttribute reference = propertyInfo.GetCustomAttributes().OfType<ReferenceAttribute>().FirstOrDefault();
                    if (reference != null)
                    {
                        foreignKeysList.Add(new ForeignKey(tableName, reference.TableReference, propertyInfo.Name + "Id", reference.RowReference));
                    }
                }
            }

            members = membersList.ToArray();
            foreignKeys = foreignKeysList.ToArray();
        }
        
        public Type GetMemberType()
        {
            if (_realType == null) _realType = Type.GetType(modelType);
            return _realType;
        }

        public object CreateModelByRow(DataRow dataRow)
        {
            var type = GetMemberType();
            object instance = System.Activator.CreateInstance(type);
            for (int i = 0; i < members.Length; i++)
            {
                PropertyInfo property = type.GetProperty(members[i].name);
                var value = dataRow[i];
                var isNull = value is DBNull;
                if (value is long l)
                {
                    value = (int) l;
                }
                property.SetValue(instance, isNull ? null : value);
            }

            return instance;
        }
    }

    [Serializable]
    public class Member
    {
        private static Dictionary<Type, string> Types = new()
        {
            {typeof(int), "INTAGER"},
            {typeof(float), "FLOAT"},
            {typeof(string), "TEXT"},
        };
        
        public string name;
        public string sqlType;
        public bool canBeNull;
        public bool isPrimaryKey;

        public Member(PropertyInfo propertyInfo)
        {
            name = propertyInfo.Name;
            var propertyType = propertyInfo.PropertyType;
            foreach (Attribute customAttribute in propertyInfo.GetCustomAttributes())
            {
                switch (customAttribute)
                {
                    case DataTypeAttribute dataTypeAttribute:
                        sqlType = dataTypeAttribute.Type;
                        break;
                    case PrimaryKeyAttribute:
                        isPrimaryKey = true;
                        break;
                }   
            }

            bool isNullable = propertyType.IsGenericType &&
                              propertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
            
            if(string.IsNullOrEmpty(sqlType))
            {
                if (isNullable)
                {
                    Type underlyingType = Nullable.GetUnderlyingType(propertyType);
                    sqlType = Types[underlyingType];
                }
                else
                {
                    sqlType = Types[propertyInfo.PropertyType];
                }
            }

            canBeNull = !isPrimaryKey && (isNullable || IsNullableType(propertyInfo.PropertyType));
        }

        private static bool IsNullableType(Type type) => !type.IsPrimitive && !type.IsValueType && !type.IsEnum;
    }

    [Serializable]
    public class ForeignKey
    {
        public string originTable;
        public string destinationTable;
        public string originMember;
        public string destinationMember;

        public ForeignKey(string originTable, string destinationTable, string originMember, string destinationMember)
        {
            this.originTable = originTable;
            this.destinationTable = destinationTable;
            this.originMember = originMember;
            this.destinationMember = destinationMember;
        }
    }
}