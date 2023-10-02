using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ara3D;
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
        
        private Func<float, double> PreviewScaleTransform = v => Mathf.Lerp(4488000000, 448800000000, v);
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
                serializedTree.stringValue = _serializer.Serialize(_container);
                _serializedObject.ApplyModifiedProperties();
            }

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
            /*foreach (IMass mass in _container.Root.GetRecursively())
            {
                if(mass == null) continue; 
                _viewsPerMass[mass].transform.localPosition = _trajectories[mass].GetPosition(0d) * PreviewScaleTransform(PreviewScaleValue);
            }*/
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
            if (_container.Root != null)
            {
                foreach (IMass mass in _container.Root.GetContent())
                {
                    if(mass == null) continue;
                    DrawTrajectoriesRecursively(mass, DVector3.Zero);
                }
            }
        }

        private void DrawTrajectoriesRecursively(IMass mass, DVector3 origin)
        {
            if(mass == null) return;
            if(!_trajectories.TryGetValue(mass, out RelativeTrajectory trajectory)) return;
            
            DrawTrajectory(trajectory, mass is CelestialBody, origin, out DVector3 output);
            foreach (IMass child in mass.GetContent())
            {
                DrawTrajectoriesRecursively(child, output);
            }
        }

        private void DrawTrajectory(RelativeTrajectory trajectory, bool drawSphere, DVector3 inputOrigin, out DVector3 outputOrigin)
        {
            DVector3 localPosition = trajectory.GetPosition(_time);
            outputOrigin = localPosition + inputOrigin;
            float scale = (float)PreviewScaleTransform(PreviewScaleValue);
            Vector3 sOutput = (Vector3) (outputOrigin) / scale;
            Vector3 sInput = (Vector3) (inputOrigin) / scale;
            
            Quaternion handleRotationCorrection = Quaternion.Euler(-90, 0, 0);
            Quaternion trajectoryRotation = Quaternion.Euler(Mathf.Rad2Deg * (float) trajectory.LatitudeShift, Mathf.Rad2Deg * (float) trajectory.LongitudeShift, Mathf.Rad2Deg * (float) trajectory.Inclination);
           
            Matrix4x4 figureMatrix = Matrix4x4.TRS(new Vector3(0, 0, (float)((trajectory.PericenterRadius - trajectory.SemiMajorAxis) / scale)), handleRotationCorrection, new Vector3((float)trajectory.SemiMinorAxis, (float)trajectory.SemiMajorAxis, 0) / scale);
            Matrix4x4 worldMatrix = Matrix4x4.TRS(sInput, trajectoryRotation, Vector3.one);
            
            Handles.matrix = worldMatrix * figureMatrix;
            Handles.CircleHandleCap(-1, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
            Handles.matrix = Matrix4x4.identity;
            Handles.DrawDottedLine(sOutput, sInput, 10);
            if(drawSphere) Handles.SphereHandleCap(-1, sOutput, Quaternion.identity, 0.1f * HandleUtility.GetHandleSize(sOutput), EventType.Repaint);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(serializedTree);
            EditorGUI.BeginChangeCheck();
            PreviewScaleValue = EditorGUILayout.Slider("Preview scale", PreviewScaleValue, 0f, 1f);
            _time = EditorGUILayout.Slider("Time", _time, 0, 365*24*3600);
            if (EditorGUI.EndChangeCheck())
            {
                OnSceneGUI();
            }
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