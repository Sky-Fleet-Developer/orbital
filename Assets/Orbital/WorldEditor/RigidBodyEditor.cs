using System;
using Ara3D;
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
    [CustomEditor(typeof(RigidBodySystemComponent))]
    public class RigidBodyEditor : Editor
    {
        private SerializedObject _serializedObject;

        // ReSharper disable once InconsistentNaming
        private SerializedProperty variables;
        private GameObject _targetGameObject;
        private RigidBodySystemComponent _rigidBody;
        private MassSystemComponent _parent;
        private RelativeTrajectory _trajectory;
        private TreeContainer _tree;
        private Func<float, double> PreviewScaleTransform = v => 1 / Mathf.Lerp(448800000, 448800000000, v);
        private readonly ISerializer _serializer = new JsonPerformance();
        private TimeService _timeService;
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

        private void OnEnable()
        {
            variables = serializedObject.FindProperty(nameof(variables));
            _targetGameObject = ((MonoBehaviour) target).gameObject;
            _rigidBody = (RigidBodySystemComponent) target;
            World world = _targetGameObject.GetComponentInParent<World>();
            if(!world) return;
            _timeService = FindObjectOfType<TimeService>();
            string treeString = new SerializedObject(world).FindProperty("tree").FindPropertyRelative("serializedValue").stringValue;
            _tree = _serializer.Deserialize<TreeContainer>(treeString);
            _tree.CalculateForRoot(world.transform);
            _parent = _targetGameObject.GetComponentInParent<MassSystemComponent>();
            _trajectory = new RelativeTrajectory(_rigidBody, _tree._componentPerMass[_parent], SystemType.RigidBody);
            _trajectory.Calculate();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(variables);
            GUILayout.Box($"Eccentricity: {_trajectory.Eccentricity}");
            GUILayout.Box($"Semi major axis: {_trajectory.SemiMajorAxis}");
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _trajectory.Calculate();
            }
        }

        private void OnSceneGUI()
        {
            double time = _timeService != null ? _timeService.WorldTime : 0;
            float scale = (float) PreviewScaleTransform(PreviewScaleValue);
            _tree.DrawTrajectories(time, scale);
            DVector3 parentPosition = _tree.GetGlobalPosition(_parent, time);
            TrajectoryEditorUtility.DrawTrajectory(_trajectory, time, scale, true
                , parentPosition, out DVector3 output);
        }
    }
#endif
}