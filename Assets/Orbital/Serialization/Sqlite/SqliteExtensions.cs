using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Data.Sqlite;
using UnityEngine;

namespace Orbital.Core.Serialization.Sqlite
{
    public static class SqliteExtensions
    {
        private const char Space = ' ';
        private const string Comma = ", ";
        private const string StaplesOpen = " (";
        private const string StaplesClose = ") ";
        private const string Nullable = " NULL";
        private const string NotNullable = " NOT NULL";
        private const string PrimaryKey = " PRIMARY KEY";
        private const string ForeignKeyFormat = " CONSTRAINT[FK_{0}_{1}_{2}] FOREIGN KEY ([{2}]) REFERENCES [{1}] ([{3}])";
        private const string CreateTableRoot = "CREATE TABLE IF NOT EXISTS ";
        private const string SelectTableRootFormat = "SELECT {0} FROM ";
        private const string UpdateTableFormat = "UPDATE {0} SET ({1}) = ({2}) WHERE (Id = {3});";
        private const string InsertFormat = "INSERT INTO {0} ({1}) VALUES ({2});";
        private const string DeleteFormat = "DELETE FROM {0} WHERE Id = {1};";
        private const string All = "* ";
        private const string WhereInFormat = " WHERE {0} IN ()";
        private const string WhereFormat = " WHERE {0}";
        //0 - fields, 1 - table name, 
        private const string RecursiveFormat = " WHERE {0}";

        private static Dictionary<SqliteConnection, RequestBuilder> _requestBuilders = new Dictionary<SqliteConnection, RequestBuilder>();

        private static RequestBuilder GetOrCreateBuilder(SqliteConnection connection)
        {
            if (!_requestBuilders.TryGetValue(connection, out var requestBuilder))
            {
                requestBuilder = new RequestBuilder();
                _requestBuilders.Add(connection, requestBuilder);
            }
            return requestBuilder;
        }
        
        public static void CreateTable<T>(this SqliteConnection connection, string tableName, Declaration declaration)
        {
            Type type = typeof(T);
            var model = declaration.SetDeclaration(type, tableName);
            
            
            StringBuilder sqlBuilder = new StringBuilder();
            sqlBuilder.Append(CreateTableRoot);
            sqlBuilder.Append(model.tableName);
            sqlBuilder.Append(StaplesOpen);
            sqlBuilder.Append(BuildProperties(model, true));
            sqlBuilder.Append(Comma);
            sqlBuilder.Append(BuildForeignKeys(model));
            sqlBuilder.Append(StaplesClose);
            var sql = sqlBuilder.ToString();
            Debug.Log(sql);
            using (SqliteCommand command = new SqliteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static string BuildForeignKeys(ModelType modelType)
        {
            StringBuilder builder = new StringBuilder();
            foreach (ForeignKey modelForeignKey in modelType.foreignKeys)
            {
                AppendForeignKey(builder, modelForeignKey);
                builder.Append(Comma);
            }
            builder.Remove(builder.Length - 2, 1);
            return builder.ToString();
        }
        private static void AppendForeignKey(StringBuilder memberBuilder, ForeignKey foreignKey)
        {
            memberBuilder.Append(string.Format(ForeignKeyFormat, foreignKey.originTable, foreignKey.destinationTable, foreignKey.originMember, foreignKey.destinationMember));
        }

        private static string BuildProperties(ModelType modelType, bool needAppendDeclaration)
        {
            StringBuilder builder = new StringBuilder();
            if (needAppendDeclaration)
            {
                foreach (Member modelMember in modelType.members)
                {
                    AppendPropertyWithDeclaration(builder, modelMember);
                    builder.Append(Comma);
                }
            }
            else
            {
                foreach (Member modelMember in modelType.members)
                {
                    builder.Append(modelMember.name);
                    builder.Append(Comma);
                }
            }
            builder.Remove(builder.Length - 2, 1);
            return builder.ToString();
        }
        private static void AppendPropertyWithDeclaration(StringBuilder memberBuilder, Member member)
        {
            memberBuilder.Append(member.name);
            memberBuilder.Append(Space);

            memberBuilder.Append(member.sqlType);
            if(member.isPrimaryKey) memberBuilder.Append(PrimaryKey);

            memberBuilder.Append(member.canBeNull ? Nullable : NotNullable);
        }

        private static string BuildValues<T>(ModelType myType, T source) where  T : ModelBase
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Type type = typeof(T);
            StringBuilder builder = new StringBuilder();
            foreach (Member modelMember in myType.members)
            {
                var value = modelMember.GetValueFrom(source, type);
                builder.Append(value ?? "NULL");
                builder.Append(Comma);
            }
            builder.Remove(builder.Length - 2, 1);
            return builder.ToString();
        }

        // SELECT * FROM TableName
        /*public static List<T> GetTable<T>(this SqliteConnection connection, Declaration declaration)
        {
            var model = declaration.GetModel(typeof(T));
            string sql = Select(model);
            DataSet dataSet = Request(connection, sql);

            List<T> result = new List<T>();
            var rows = dataSet.Tables[0].Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                result.Add((T)model.CreateModelByRow(rows[i]));
            }

            return result;
        }*/

        private static DataSet Request(SqliteConnection connection, string sql)
        {
            Debug.Log(sql);
            DataSet dataSet = new DataSet();
            using (SqliteCommand command = new SqliteCommand(sql, connection))
            {
                using (SqliteDataAdapter dataAdapter = new SqliteDataAdapter(command))
                {
                    dataAdapter.Fill(dataSet);
                }
            }

            return dataSet;
        }

        public static SqliteConnection Select(this SqliteConnection connection, string tableName, string whatToSelect = All)
        {
            var builder = GetOrCreateBuilder(connection);
            builder.Parts.Add((sqlBuilder, pointer) =>
            {
                sqlBuilder.Insert(ref pointer, string.Format(SelectTableRootFormat, whatToSelect));
                sqlBuilder.Insert(ref pointer, tableName);
                return 1;
            });
            return connection;
        }

        public static SqliteConnection WhereIn(this SqliteConnection connection, string fieldName)
        {
            var builder = GetOrCreateBuilder(connection);
            builder.Parts.Add((sqlBuilder, pointer) =>
            {
                sqlBuilder.Insert(ref pointer, string.Format(WhereInFormat, fieldName));
                return -1;
            });
            return connection;
        }
        
        public static SqliteConnection Where(this SqliteConnection connection, string expression)
        {
            var builder = GetOrCreateBuilder(connection);
            builder.Parts.Add((sqlBuilder, pointer) =>
            {
                sqlBuilder.Insert(ref pointer, string.Format(WhereFormat, expression));
                return 0;
            });
            return connection;
        }
        
        public static SqliteConnection Recursive(this SqliteConnection connection, string expression)
        {
            var builder = GetOrCreateBuilder(connection);
            builder.Parts.Add((sqlBuilder, pointer) =>
            {
                sqlBuilder.Insert(ref pointer, string.Format(WhereFormat, expression));
                return 0;
            });
            return connection;
        }

        // SELECT * FROM TableName
        public static SqliteConnection GetTable<T>(this SqliteConnection connection, TableSet<T> tableSet, Declaration declaration) where T : ModelBase
        {
            ModelType model = declaration.GetModel(typeof(T));
            var builder = GetOrCreateBuilder(connection);

            builder.Request = sql =>
            {
                DataSet dataSet = Request(connection, sql);
                tableSet.Fill(dataSet.Tables[0], model);
            };
            return connection;
        }

        /*public static SqlConnection Where(this SqliteConnection connection, )
        {
            var builder = GetOrCreateBuilder(connection);
            builder.Additional.Add(sql =>
            {
                return sql + 
            });
        }*/

        public static void Run(this SqliteConnection connection)
        {
            GetOrCreateBuilder(connection).Run();
        }

        //UPDATE TestTable SET (Name, Email) = ('ooo', 'op') WHERE (Id = 1) 
        public static void Update<T>(this SqliteConnection connection, TableSet<T> tableSet, Declaration declaration) where T : ModelBase
        {
            if (!tableSet.HasChanges())
            {
                Debug.Log($"{tableSet.TableName} has no changes and will not to be updated");
                return;
            }
            ModelType model = declaration.GetModel(typeof(T));
            StringBuilder builder = new StringBuilder();
            foreach ((DifferenceType differenceType, T element, int id) in tableSet.GetDifference(model))
            {
                switch (differenceType)
                {
                    case DifferenceType.Update:
                        builder.AppendLine(string.Format(UpdateTableFormat, tableSet.TableName, BuildProperties(model, false), BuildValues(model, element), id));
                        break;
                    case DifferenceType.Add:
                        builder.AppendLine(string.Format(InsertFormat, tableSet.TableName, BuildProperties(model, false), BuildValues(model, element)));
                        break;
                    case DifferenceType.Remove:
                        builder.AppendLine(string.Format(DeleteFormat, tableSet.TableName, id));
                        break;
                }
            }
            
            var sql = builder.ToString();
            Debug.Log(sql);
            using (SqliteCommand command = new SqliteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static void Insert(this StringBuilder stringBuilder, ref int pointer, string value)
        {
            stringBuilder.Insert(pointer, value);
            pointer += value.Length;
        }
        
        private class RequestBuilder
        {
            public Action<string> Request;

            public List<Func<StringBuilder, int, int>> Parts = new ();
            public void Run()
            {
                StringBuilder sqlBuilder = new StringBuilder();
                int pointer = 0;
                foreach (Func<StringBuilder, int, int> part in Parts)
                {
                    int oldLength = sqlBuilder.Length;
                    if (pointer > oldLength) pointer = oldLength;
                    if (pointer < 0) pointer = 0;
                    int offset = part(sqlBuilder, pointer);
                    pointer += offset + sqlBuilder.Length - oldLength;
                }
                Request(sqlBuilder.ToString());
                
                Parts.Clear();
            }
        }
    }

    
}
