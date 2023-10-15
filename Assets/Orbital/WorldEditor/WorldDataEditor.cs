using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ara3D;
using Core.Patterns.State;
using Orbital.Core;
using Orbital.Core.Serialization;
using Orbital.Core.TrajectorySystem;
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
        private readonly ISerializer _serializer = new JsonPerformance();

        private IMassSystem _currentEdit;
        private IMassSystem _currentParent;

        private Func<float, double> PreviewScaleTransform = v => 1 / Mathf.Lerp(224400000, 448800000000, v);
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
            if (_tree == null)
            {
                _tree = new TreeContainer();
            }
            _tree.CalculateForRoot(_tRoot);
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
            _tree.FillTrajectories();
        }

        private void OnSceneGUI()
        {
            if(_currentState == null) return;
            _currentState.OnSceneGui();
            _tree.DrawTrajectories(TimeService.WorldTime, (float)PreviewScaleTransform(PreviewScaleValue));
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