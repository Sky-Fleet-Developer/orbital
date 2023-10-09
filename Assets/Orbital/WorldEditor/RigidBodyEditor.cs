using System;
using Ara3D;
using Orbital.Model;
using Orbital.Model.Serialization;
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
        private SerializedProperty settings;
        private GameObject _targetGameObject;
        private RigidBodySystemComponent _rigidBody;
        private MassSystemComponent _parent;
        private RelativeTrajectory _trajectory;
        private TreeContainer _tree;
        private Func<float, double> PreviewScaleTransform = v => 1 / Mathf.Lerp(448800000, 448800000000, v);
        private readonly ISerializer _serializer = new JsonPerformance();
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
            settings = serializedObject.FindProperty(nameof(settings));
            _targetGameObject = ((MonoBehaviour) target).gameObject;
            _rigidBody = (RigidBodySystemComponent) target;
            World world = _targetGameObject.GetComponentInParent<World>();
            if(!world) return;
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
            EditorGUILayout.PropertyField(settings);
            GUILayout.Box($"Eccentricity: {_trajectory.Eccentricity}");
            GUILayout.Box($"Semi major axis: {_trajectory.SemiMajorAxis}");
            GUILayout.Box($"Start velocity: {_trajectory.GetVelocity(0)}");

            DVector3 p1 = _trajectory.GetPosition(0);
            DVector3 p2 = _trajectory.GetPosition(0.5);
            GUILayout.Box($"P': {(p2 - p1) / 0.5}");

            
            /*if (GUILayout.Button("Test"))
            {
                Test();
            }*/
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _trajectory.Calculate();
            }
        }

        /*private void Test()
        {
            var settings = _rigidBody.Variables.trajectorySettings;
            settings.SetupFromSimulation(_trajectory.GetPosition(0), _trajectory.GetVelocity(0), _parent.Mass);
        }*/

        private void OnSceneGUI()
        {
            double time = TimeService.WorldTime;
            float scale = (float) PreviewScaleTransform(PreviewScaleValue);
            _tree.DrawTrajectories(time, scale);
            DVector3 parentPosition = _tree.GetGlobalPosition(_parent, time);
            TrajectoryEditorUtility.DrawTrajectory(_trajectory, time, scale, true
                , parentPosition, out DVector3 output);
        }
    }
#endif
}