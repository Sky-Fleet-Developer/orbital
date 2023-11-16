using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Orbital.Core.Serialization.Sqlite
{
    
    [Serializable]
    public class ModelType
    {
        public string tableName;
        public Member[] members;
        public ForeignKey[] foreignKeys;
        public string myType;
        private Type _realType;
        
        public ModelType(Type modelType, string tableName)
        {
            this.tableName = tableName;
            this.myType = modelType.AssemblyQualifiedName;
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
            if (_realType == null) _realType = Type.GetType(myType);
            return _realType;
        }

        public object CreateModelByRow(DataRow dataRow)
        {
            var type = GetMemberType();
            ModelBase instance = (ModelBase)System.Activator.CreateInstance(type);
            for (int i = 0; i < members.Length; i++)
            {
                members[i].SetValueTo(instance, type, dataRow[i]);
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
            {typeof(double), "DOUBLE"},
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

            bool isNullable = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
            
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

        public object GetValueFrom(ModelBase element, Type type)
        {
            PropertyInfo property = type.GetProperty(name);
            return property.GetValue(element);
        }

        public void SetValueTo(ModelBase element, Type type, object tableValue)
        {
            PropertyInfo property = type.GetProperty(name);
            var isNull = tableValue is DBNull;
            if (tableValue is long l)
            {
                tableValue = (int) l;
            }
            property.SetValue(element, isNull ? null : tableValue);
        }
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