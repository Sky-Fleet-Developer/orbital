using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Patterns.State;
using Orbital.Controllers.Data;
using Orbital.Model.TrajectorySystem;
using Orbital.WorldEditor.SystemData;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(WorldData))]
    public partial class WorldDataEditor : Editor, IStateMaster
    {
        private const int TabulationOffset = 70;
        private const int DefaultButtonSize = 120;
        private SerializedObject _serializedObject;

        // ReSharper disable once InconsistentNaming
        private SerializedProperty serializedTree;
        private TreeContainer _container;
        private GameObject _rootObject;
        private List<Transform> _viewObjects = new();
        private Dictionary<IMass, Transform> _viewsPerMass = new();
        private readonly ISerializer _serializer = new JsonPerformance();

        State IStateMaster.CurrentState
        {
            get => _currentState;
            set => _currentState = (WorldDataState) value;
        }

        private WorldDataState _currentState;

        private void ApplyChanges()
        {
            Undo.RecordObject(target, "Change tree");
            serializedTree.stringValue = _serializer.Serialize(_container);
            _serializedObject.ApplyModifiedProperties();
            ReconstructTreeView();
            EditorUtility.SetDirty(target);
        }

        private void OnEnable()
        {
            _serializedObject = new SerializedObject(target);
            serializedTree = _serializedObject.FindProperty(nameof(serializedTree));
            _rootObject = ((MonoBehaviour) target).gameObject;
            _container = _serializer.Deserialize<TreeContainer>(serializedTree.stringValue);
            if (_container == null)
            {
                _container = new TreeContainer();
                _currentState = new CreateRootState(this);
            }
            else
            {
                if (ReconstructTreeView())
                {
                    _currentState = new ExtendTreeState(this);
                }
                else
                {
                    _currentState = new CreateRootState(this);
                }
            }

            _viewObjects = new List<Transform>();
        }

        private bool ReconstructTreeView()
        {
            if (_container.Root == null) return false;

            string rootMassName = "RootMass";
            Transform root = _rootObject.transform.Find(rootMassName);
            if (!root)
            {
                root = MakeNewObject(rootMassName, _rootObject.transform, false);
            }

            ReconstructMass(_container.Root, root);
            _viewsPerMass = _container.Root.GetMap(root);
            foreach (IMass mass in _container.Root.GetRecursively())
            {
                if (_viewsPerMass[mass].TryGetComponent<CelestialSystemData>(out var value))
                {
                    value.SetSettings(mass.Settings);
                }
            }
            return true;
        }

        private void ReconstructMass(IMass mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMass mass in mRoot.GetContent())
            {
                i++;
                if(mass == null) continue;
                string wantedName = $"Child[{i}]";
                Transform tChild = tRoot.Find(wantedName);
                if (tChild == null)
                {
                    tChild = MakeNewObject(wantedName, tRoot, mass is CelestialBody);
                }

                if (!_viewObjects.Contains(tChild))
                {
                    _viewObjects.Add(tChild);
                }

                ReconstructMass(mass, tChild);
            }
        }

        private Transform MakeNewObject(string name, Transform parent, bool isCelestial)
        {
            Transform newObject = new GameObject(name, isCelestial ? new [] {typeof(BodyData), typeof(CelestialSystemData)} : new Type[0]).transform;
            _viewObjects.Add(newObject);
            newObject.SetParent(parent);
            return newObject;
        }

        private void OnSceneGUI()
        {
            _currentState.OnSceneGui();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(serializedTree);
            _currentState.Update();
        }

        private static void BeginTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(TabulationOffset);
            GUILayout.BeginVertical();
        }
        private static void EndTab()
        {
            GUILayout.EndVertical();
            GUILayout.Space(TabulationOffset);
            GUILayout.EndHorizontal();
        }


        private interface INestedStateUser<in T>
        {
            void NestedStateCallback(T value);
        }

        [Flags]
        private enum PossibleMass
        {
            Null = 1,
            Celestial = 2,
            Single = 4,
            Dual = 8,
            All = 1 | 2 | 4 | 8
        }
    }
#endif
}