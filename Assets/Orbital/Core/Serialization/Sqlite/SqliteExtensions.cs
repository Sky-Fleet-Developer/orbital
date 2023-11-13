using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
        private const string All = "* ";
        
        public static void CreateTable<T>(this SqliteConnection connection, string tableName, Declaration declaration)
        {
            Type type = typeof(T);
            var model = declaration.SetDeclaration(type, tableName);
            List<string> members = new List<string>();
            AppendProperties(model, members, true);
            AppendForeignKeys(model, members);
            
            StringBuilder sqlBuilder = new StringBuilder();
            sqlBuilder.Append(CreateTableRoot);
            sqlBuilder.Append(model.tableName);
            sqlBuilder.Append(StaplesOpen);
            AppendBody(members, sqlBuilder);
            sqlBuilder.Append(StaplesClose);
            var sql = sqlBuilder.ToString();
            Debug.Log(sql);
            using (SqliteCommand command = new SqliteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }


        private static void AppendBody(List<string> members, StringBuilder sqlBuilder)
        {
            for (var i = 0; i < members.Count; i++)
            {
                sqlBuilder.Append(members[i]);
                if (i < members.Count - 1)
                {
                    sqlBuilder.Append(Comma);
                }
            }
        }

        private static void AppendForeignKeys(Model model, List<string> members)
        {
            foreach (ForeignKey modelForeignKey in model.foreignKeys)
            {
                StringBuilder memberBuilder = new StringBuilder();
                AppendForeignKey(memberBuilder, modelForeignKey);
                members.Add(memberBuilder.ToString());
            }
        }

        private static void AppendProperties(Model model, List<string> members, bool needAppendDeclaration)
        {
            foreach (Member modelMember in model.members)
            {
                StringBuilder memberBuilder = new StringBuilder();
                AppendProperty(memberBuilder, modelMember, needAppendDeclaration);
                members.Add(memberBuilder.ToString());
            }
        }
        
        /*private static void AppendValues(Model model, List<string> members)
        {
            foreach (Member modelMember in model.members)
            {
                StringBuilder memberBuilder = new StringBuilder();
                AppendProperty(memberBuilder, modelMember, false);
                members.Add(memberBuilder.ToString());
            }
        }*/

        // SELECT * FROM TableName
        public static List<T> GetTable<T>(this SqliteConnection connection, Declaration declaration)
        {
            var model = declaration.GetModel(typeof(T));
            
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

            List<T> result = new List<T>();
            var rows = dataSet.Tables[0].Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                result.Add((T)model.CreateModelByRow(rows[i]));
            }

            return result;
        }

        private static void AppendForeignKey(StringBuilder memberBuilder, ForeignKey foreignKey)
        {
            memberBuilder.Append(string.Format(ForeignKeyFormat, foreignKey.originTable, foreignKey.destinationTable, foreignKey.originMember,
                foreignKey.destinationMember));
        }

        private static void AppendProperty(StringBuilder memberBuilder, Member member, bool needAppendDeclaration)
        {
            memberBuilder.Append(member.name);
            if(!needAppendDeclaration) return;
            memberBuilder.Append(Space);

            memberBuilder.Append(member.sqlType);
            if(member.isPrimaryKey) memberBuilder.Append(PrimaryKey);

            memberBuilder.Append(member.canBeNull ? Nullable : NotNullable);
        }
    }
}
