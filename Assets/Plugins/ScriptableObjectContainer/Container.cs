using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectContainer
{
    [CreateAssetMenu(fileName = "NewContainer", menuName = "ScriptableObject Container")]
    public class Container : ScriptableObject
    {
        [SerializeField] private ScriptableObject[] nestedAssets;

        public IEnumerable<T> GetAssetsAtType<T>() where T : ScriptableObject => nestedAssets.OfType<T>();

        #if UNITY_EDITOR
        [ShowInInspector, ValueDropdown("GetTypesVariants"), PropertyOrder(0)]
        private Type _assetToAdd;
        [ShowInInspector] private string _newAssetName;
        private IEnumerable<Type> GetTypesVariants()
        {
            Type parentType = typeof(ScriptableObject);
            if (string.IsNullOrEmpty(_newAssetName) && _assetToAdd != null)
            {
                _newAssetName = _assetToAdd.Name;
                int i = 1;
                while (nestedAssets.FirstOrDefault(x => x.name == _newAssetName) != null)
                {
                    _newAssetName = $"{_assetToAdd.Name}_{i++}";
                }
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(x => x.IsSubclassOf(parentType)))
                {
                    yield return type;
                }
            }
        }
        
        [Button(ButtonSizes.Medium), PropertyOrder(1)]
        private void AddAsset()
        {
            if (_assetToAdd != null)
            {
                var instance = CreateInstance(_assetToAdd);
                instance.name = _newAssetName;
                AddNestedAsset(instance);
                _newAssetName = string.Empty;
            }
        }
        /// Editor only!
        public void AddNestedAsset(ScriptableObject asset)
        {
            if (asset != null)
            {
                if (nestedAssets == null)
                {
                    nestedAssets = new ScriptableObject[1];
                    nestedAssets[0] = asset;
                }
                else
                {
                    int length = nestedAssets.Length;
                    ScriptableObject[] newArray = new ScriptableObject[length + 1];
                    for (int i = 0; i < length; i++)
                    {
                        newArray[i] = nestedAssets[i];
                    }

                    newArray[length] = asset;
                    nestedAssets = newArray;
                }

                AssetDatabase.AddObjectToAsset(asset, this);
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssetIfDirty(this);
            }
        }

        [ShowInInspector, ValueDropdown("GetRemoveVariants"), PropertyOrder(2)]
        private string _toRemove;

        private IEnumerable<string> GetRemoveVariants()
        {
            foreach (ScriptableObject scriptableObject in nestedAssets)
            {
                yield return scriptableObject.name;
            }

            yield return "none";
        }

        [Button(ButtonSizes.Medium), PropertyOrder(3)]
        private void Remove()
        {
            if(_toRemove != "none") RemoveNestedAsset(nestedAssets.First(x => x.name == _toRemove));
        }
        
        /// Editor only!
        public void RemoveNestedAsset(ScriptableObject asset)
        {
            if (asset != null && nestedAssets != null)
            {
                int index = System.Array.IndexOf(nestedAssets, asset);
                if (index != -1)
                {
                    int length = nestedAssets.Length;
                    ScriptableObject[] newArray = new ScriptableObject[length - 1];
                    for (int i = 0, j = 0; i < length; i++)
                    {
                        if (i != index)
                        {
                            newArray[j++] = nestedAssets[i];
                        }
                    }

                    nestedAssets = newArray;
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    DestroyImmediate(asset);
                    EditorUtility.SetDirty(this);
                    AssetDatabase.SaveAssetIfDirty(this);
                }
            }
        }
#endif
    }
}