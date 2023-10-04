using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ara3D;
using Core.Patterns.State;
using Orbital.Model;
using Orbital.Model.Serialization;
using Orbital.Model.Services;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(World))]
    public partial class WorldDataEditor : Editor, IStateMaster
    {
        private const int TabulationOffset = 70;
        private const int DefaultButtonSize = 120;

        // ReSharper disable once InconsistentNaming
        private SerializedProperty serializedTree;
        private TreeContainer _tree;
        private Transform _tRoot;
        private readonly Dictionary<IMassSystem, RelativeTrajectory> _trajectories = new();
        private readonly ISerializer _serializer = new JsonPerformance();

        private IMassSystem _currentEdit;
        private IMassSystem _currentParent;
        private TimeService _timeService;

        private Func<float, double> PreviewScaleTransform = v => 1 / Mathf.Lerp(448800000, 448800000000, v);
        private float PreviewScaleValue
        {
            get => _previewScaleValue;
            set
            {
                _previewScaleValue = value;
                PlayerPrefs.SetFloat(PreviewScaleKey, value);
            }
        }
        private float _previewScaleValue;
        private const string PreviewScaleKey = "PREVIRE_SCALE_EDITOR";
        private float _time = 0;
        State IStateMaster.CurrentState
        {
            get => _currentState;
            set => _currentState = (WorldDataState) value;
        }

        private WorldDataState _currentState;

        private void ApplyChanges(bool serialize = true)
        {
            Undo.RecordObject(target, "Change tree");
            if (serialize)
            {
                serializedTree.stringValue = _serializer.Serialize(_tree);
                serializedObject.ApplyModifiedProperties();
            }
            RefreshTrajectories();
            EditorUtility.SetDirty(target);
        }

        private void OnEnable()
        {
            serializedTree = serializedObject.FindProperty("tree").FindPropertyRelative("serializedValue");
            _tRoot = ((MonoBehaviour) target).transform;
            _tree = _serializer.Deserialize<TreeContainer>(serializedTree.stringValue);
            _tree.CalculateForRoot(_tRoot);
            _timeService = FindObjectOfType<TimeService>();
            PreviewScaleValue = PlayerPrefs.GetFloat(PreviewScaleKey, 0);
            if (_tree == null)
            {
                _tree = new TreeContainer();
                _currentState = new CreateRootState(this);
            }
            else
            {
                if (_tree.Root == null)
                {
                    _currentState = new CreateRootState(this);
                }
                else
                {
                    _currentState = new ExtendTreeState(this);
                    RefreshTrajectories();
                }
            }
        }
        
        private void RefreshTrajectories()
        {
            _tree.Root.FillTrajectoriesRecursively(_trajectories);
        }

        private void OnSceneGUI()
        {
            _currentState.OnSceneGui();
            double time = _timeService != null ? _timeService.WorldTime : 0;
            _tree.DrawTrajectories(time, (float)PreviewScaleTransform(PreviewScaleValue));
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedTree);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Change tree");
                serializedObject.ApplyModifiedProperties();
                RefreshTrajectories();
                EditorUtility.SetDirty(target);
            }
            if (GUILayout.Button("Refresh hierarchy", GUILayout.Width(DefaultButtonSize)))
            {
                RefreshHierarchy();
            }
            PreviewScaleValue = EditorGUILayout.Slider("Preview scale", PreviewScaleValue, 0f, 1f);
            _time = EditorGUILayout.Slider("Time", _time, 0, 365*24*3600);
            _currentState.Update();
        }

        private void RefreshHierarchy()
        {
            _tree.CalculateForRoot(_tRoot);
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
            void NestedStateCallback(T value, bool final = true);
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