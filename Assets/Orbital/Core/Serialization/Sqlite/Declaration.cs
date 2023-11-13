using System.Collections.Generic;
using System.Linq;
using ModestTree;
using UnityEditor;
using UnityEngine;

namespace Orbital.Core.Serialization.Sqlite
{
    [CreateAssetMenu(menuName = "DB/Declaration")]
    public class Declaration : ScriptableObject
    {
        [SerializeField] private List<Model> modelTypes;

        
        public Model SetDeclaration(System.Type modelType, string tableName)
        {
            #if UNITY_EDITOR
            Undo.RecordObject(this, "modify sql declaration");
            #endif
            var model = new Model(modelType, tableName);
            int lastIdx = modelTypes.FindIndex(x => x.tableName == tableName);
            if (lastIdx != -1)
            {
                modelTypes[lastIdx] = model;
            }
            else
            {
                modelTypes.Add(model);
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
            return model;
        }

        public Model GetModel(System.Type modelType)
        {
            string typeName = modelType.AssemblyQualifiedName;
            return modelTypes.Find(x => x.modelType == typeName);
        }
    }
}
