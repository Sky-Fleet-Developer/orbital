using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ara3D;
using UnityEngine;
using Mono.Data.Sqlite;
using Sirenix.OdinInspector;


namespace Orbital.Core.Serialization
{
    public class WorldSqlDataAdapter : MonoBehaviour
    {
        [SerializeField, FilePath] private string databasePath;
        
        private const string TableCreationFormat = "CREATE TABLE IF NOT EXISTS {0} ({1});";
        private const string TableInsertFormat = "INSERT INTO {0} ({1}) VALUES ({2});";
        private const string ReadTableFormat = "SELECT * FROM {0};";
        private static JsonPerformance _serializer = new JsonPerformance();
        
        public string BuildTableKeys(IEnumerable<string> collection)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string objectTableKey in collection)
            {
                stringBuilder.Append(objectTableKey);
            }
            return stringBuilder.ToString();
        }
        
        /*public string BuildTableValues()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append('(');
            foreach (string value in arguments)
            {
                stringBuilder.Append(value);
            }
            stringBuilder.Append(')');
            return stringBuilder.ToString();
        }*/

        public IEnumerable<string> GetObjectTableKeys()
        {
            yield return "Id INTAGER PRIMARY KEY NOT NULL,";
            yield return "OwnerId INTAGER NULL,";
            yield return "LocalPosition VARCHAR (100) NOT NULL,";
            yield return "LocalRotation VARCHAR (10) NOT NULL,";
            yield return "Tag VARCHAR (50) NOT NULL,";
            yield return "Layer INTAGER NOT NULL";
        }
        
        public (string keys, string values) BuildParametersForObject(Transform transform)
        {
            string keys = "Id, OwnerId, LocalPosition, LocalRotation, Tag, Layer";
            string parentId = transform.parent ? transform.parent.gameObject.GetInstanceID().ToString() : "NULL";
            string values =
                $"{transform.gameObject.GetInstanceID()}, {parentId}, '{_serializer.Serialize(transform.localPosition)}', '{_serializer.Serialize(transform.localEulerAngles)}', '{transform.gameObject.tag}', {transform.gameObject.layer}";
            return (keys, values);
        }
        
        public IEnumerable<string> GetComponentsTableKeys()
        {
            yield return "Id INTAGER PRIMARY KEY NOT NULL,";
            yield return "ObjectId INTAGER NOT NULL,";
            yield return "Type VARCHAR (150),";
            yield return "Settings VARCHAR (2000) NOT NULL,";
            yield return "Variables VARCHAR (2000) NOT NULL";
        }

        public (string keys, string values) BuildParametersForComponents(ISystemComponentAccessor toSerialize)
        {
            string keys = "Id, ObjectId, Type, Settings, Variables";
            string objectId = ((MonoBehaviour) toSerialize).gameObject.GetInstanceID().ToString();
            string values = $"{toSerialize.Id}, {objectId}, '{toSerialize.GetType().AssemblyQualifiedName}', '{_serializer.Serialize(toSerialize.Settings)}', '{_serializer.Serialize(toSerialize.Variables)}'";
            return (keys, values);
        }

        [Button]
        public void WriteWorld(World world)
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + databasePath))
            {
                WriteWorld(world, connection, world.name);
            }
        }
        
        // go + transform table: parent, position, name, etc...
        // components table: id, settings, variables
        public void WriteWorld(World world, SqliteConnection connection, string key)
        {
            connection.Open();
            string request = string.Format(TableCreationFormat, key + "Objects", BuildTableKeys(GetObjectTableKeys()));
            Debug.Log("Sql request:");
            Debug.Log(request);
            using (SqliteCommand command = new SqliteCommand(request, connection))
            {
                command.ExecuteNonQuery();
            }
            request = string.Format(TableCreationFormat, key + "Components", BuildTableKeys(GetComponentsTableKeys()));
            Debug.Log(request);
            using (SqliteCommand command = new SqliteCommand(request, connection))
            {
                command.ExecuteNonQuery();
            }

            StringBuilder objectsRequestBuilder = new StringBuilder();
            
            objectsRequestBuilder.AppendLine($"DELETE FROM {key}Objects;");
            objectsRequestBuilder.AppendLine($"DELETE FROM {key}Components;");

            Transform[] hierarchy = world.GetComponentsInChildren<Transform>();
            foreach (Transform transform in hierarchy)
            {
                if (((int)HideFlags.DontSave & (int)transform.hideFlags) == (int)HideFlags.DontSave)
                {
                    continue;
                }
                (string keys, string values) = BuildParametersForObject((transform));
                objectsRequestBuilder.AppendLine(string.Format(TableInsertFormat, key + "Objects", keys, values));
            }
            
            var components = world.gameObject.GetComponentsInChildren<ISystemComponentAccessor>();
            foreach (ISystemComponentAccessor systemComponentAccessor in components)
            {
                (string keys, string values) = BuildParametersForComponents(systemComponentAccessor);
                objectsRequestBuilder.AppendLine(string.Format(TableInsertFormat, key + "Components", keys, values));
            }

            request = objectsRequestBuilder.ToString();
            Debug.Log("Sql request:");
            Debug.Log(request);
            using (SqliteCommand command = new SqliteCommand(request, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        public static World BuildWorldFromTables(string worldName, string databasePath)
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + databasePath))
            {
                connection.Open();
                Dictionary<int, GameObject> objectsMap = new();
                List<GameObject> objectsByRow = new List<GameObject>();
                using (SqliteDataAdapter dataAdapter = new SqliteDataAdapter(string.Format(ReadTableFormat, worldName + "Objects"), connection))
                {
                    DataSet dataSet = new DataSet();
                    dataAdapter.Fill(dataSet);
                    if (dataSet.Tables[0].Rows.Count == 0) return null;

                    //Player world = new GameObject(worldName).AddComponent<Player>();

                    for (int i = 0; i < dataSet.Tables[0].Rows.Count; i++)
                    {
                        DataRow row = dataSet.Tables[0].Rows[i];
                        var newObject = new GameObject($"n_{i}");
                        var t = row[0].GetType();
                        objectsMap.Add((int)(long) row[0], newObject);
                        objectsByRow.Add(newObject);
                        newObject.tag = (string) row[4];
                        newObject.layer = (int)(long) row[5];
                    }

                    for (int i = 0; i < dataSet.Tables[0].Rows.Count; i++)
                    {
                        DataRow row = dataSet.Tables[0].Rows[i];
                        Transform tr = objectsByRow[i].transform;
                        bool isParentNull = row[1] is DBNull;
                        if (!isParentNull)
                        {
                            int parentId = (int) (long)row[1];
                            tr.SetParent(objectsMap[parentId].transform);
                        }

                        tr.localPosition = _serializer.Deserialize<DVector3>((string) row[2]);
                        tr.localEulerAngles = _serializer.Deserialize<DVector3>((string) row[3]);
                    }
                }

                using (SqliteDataAdapter dataAdapter = new SqliteDataAdapter(string.Format(ReadTableFormat, worldName + "Components"), connection))
                {
                    DataSet dataSet = new DataSet();
                    dataAdapter.Fill(dataSet);
                    for (int i = 0; i < dataSet.Tables[0].Rows.Count; i++)
                    {
                        DataRow row = dataSet.Tables[0].Rows[i];
                        GameObject target = objectsMap[(int)(long) row[1]];
                        string typename = (string) row[2];
                        var type = Type.GetType(typename);
                        ISystemComponentAccessor accessor = (ISystemComponentAccessor) target.AddComponent(type);
                        var generic = type.BaseType.GetGenericArguments();
                        accessor.Variables = _serializer.Deserialize(generic[0], (string) row[4]);
                        accessor.Settings = _serializer.Deserialize(generic[1], (string) row[3]);
                    }

                    return objectsByRow[0].AddComponent<World>();
                }
            }
        }
    }
}
