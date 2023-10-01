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
        private Dictionary<IMass, Transform> _viewsPerMass;
        private readonly Dictionary<IMass, RelativeTrajectory> _trajectories = new();
        private readonly ISerializer _serializer = new JsonPerformance();

        private IMass _currentEdit;
        private IMass _currentParent;
        
        private Func<float, double> PreviewScaleTransform = v => Mathf.Lerp(44880000000, 448800000000, v);
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

        private void ApplyChanges()
        {
            Undo.RecordObject(target, "Change tree");
            serializedTree.stringValue = _serializer.Serialize(_container);
            _serializedObject.ApplyModifiedProperties();
            ReconstructTreeView();
            RefreshTrajectories();
            EditorUtility.SetDirty(target);
        }

        private void OnEnable()
        {
            _serializedObject = new SerializedObject(target);
            serializedTree = _serializedObject.FindProperty(nameof(serializedTree));
            _rootObject = ((MonoBehaviour) target).gameObject;
            _container = _serializer.Deserialize<TreeContainer>(serializedTree.stringValue);
            PreviewScaleValue = PlayerPrefs.GetFloat(PreviewScaleKey, 0);
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
                    RefreshTrajectories();
                }
                else
                {
                    _currentState = new CreateRootState(this);
                }
            }

            _viewObjects = new List<Transform>();
        }
        
        private void RefreshTrajectories()
        {
            _container.Root.FillTrajectoriesRecursively(_trajectories);
            foreach (IMass mass in _container.Root.GetRecursively())
            {
                if(mass == null) continue; 
                _viewsPerMass[mass].transform.localPosition = _trajectories[mass].GetPosition(0d) * PreviewScaleTransform(PreviewScaleValue);
            }
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
                if(mass == null) continue;
                if (_viewsPerMass[mass].TryGetComponent<CelestialSystemData>(out CelestialSystemData value))
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
            foreach (IMass mass in _container.Root.GetRecursively())
            {
                DrawTrajectoryForMass(mass);
            }
        }

        private void DrawTrajectoryForMass(IMass mass)
        {
            RelativeTrajectory trajectory = _trajectories[mass];
            if (trajectory.IsZero) return;
            Quaternion handleRotationCorrection = Quaternion.Euler(-90, 0, 0);
            Quaternion trajectoryRotation = Quaternion.Euler(Mathf.Rad2Deg * (float) trajectory.LatitudeShift, Mathf.Rad2Deg * (float) trajectory.LongitudeShift, Mathf.Rad2Deg * (float) trajectory.Inclination);
            float scale = (float)PreviewScaleTransform(PreviewScaleValue);
            Matrix4x4 figureMatrix = Matrix4x4.TRS(new Vector3(0, 0, (float)((trajectory.SemiMajorAxis - trajectory.PericenterRadius) / scale)), handleRotationCorrection, new Vector3((float)trajectory.SemiMinorAxis, (float)trajectory.SemiMajorAxis, 0) / scale);
            Matrix4x4 worldMatrix = Matrix4x4.TRS(Vector3.zero, trajectoryRotation, Vector3.one);
            Handles.matrix = worldMatrix * figureMatrix;
            Handles.CircleHandleCap(-1, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
            Handles.matrix = worldMatrix;
            Handles.SphereHandleCap(-1, Vector3.back * ((float)trajectory.PericenterRadius / scale), Quaternion.identity, 0.1f, EventType.Repaint);
            Handles.matrix = Matrix4x4.identity;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(serializedTree);
            PreviewScaleValue = EditorGUILayout.Slider("Preview scale", PreviewScaleValue, 0f, 1f);
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