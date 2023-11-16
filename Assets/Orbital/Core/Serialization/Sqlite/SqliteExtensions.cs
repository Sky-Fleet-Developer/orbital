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
                builder.Append(modelMember.GetValueFrom(source, type) ?? "NULL");
                builder.Append(Comma);
            }
            builder.Remove(builder.Length - 2, 1);
            return builder.ToString();
        }

        // SELECT * FROM TableName
        public static List<T> GetTable<T>(this SqliteConnection connection, Declaration declaration)
        {
            var model = declaration.GetModel(typeof(T));
            
            DataSet dataSet = RequestTable(connection, model);

            List<T> result = new List<T>();
            var rows = dataSet.Tables[0].Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                result.Add((T)model.CreateModelByRow(rows[i]));
            }

            return result;
        }

        private static DataSet RequestTable(SqliteConnection connection, ModelType model)
        {
            StringBuilder sqlBuilder = new StringBuilder();
            sqlBuilder.Append(string.Format(SelectTableRootFormat, All));
            sqlBuilder.Append(model.tableName);
            var sql = sqlBuilder.ToString();
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

        // SELECT * FROM TableName
        public static void GetTable<T>(this SqliteConnection connection, TableSet<T> tableSet, Declaration declaration) where T : ModelBase
        {
            ModelType model = declaration.GetModel(typeof(T));
            DataSet dataSet = RequestTable(connection, model);
            tableSet.Fill(dataSet.Tables[0], model);
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
    }
}
