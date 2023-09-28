using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Patterns.State;
using Orbital.Controllers.Data;
using Orbital.Model.Components;
using Orbital.Model.TrajectorySystem;
using UnityEditor;
using UnityEngine;

namespace Orbital.WorldEditor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(WorldData))]
    public class WorldDataEditor : Editor, IStateMaster
    {
        private const int TabulationOffset = 0;
        private const int DefaultButtonSize = 100;
        private SerializedObject _serializedObject;

        // ReSharper disable once InconsistentNaming
        private SerializedProperty serializedTree;
        private TreeContainer _container;
        private GameObject _rootObject;
        private List<Transform> _viewObjects = new List<Transform>();
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
                root = MakeNewObject(rootMassName, _rootObject.transform);
            }

            ReconstructMass(_container.Root, root);
            return true;
        }

        private void ReconstructMass(IMass mRoot, Transform tRoot)
        {
            int i = 0;
            foreach (IMass mass in mRoot.GetContent())
            {
                string wantedName = $"Child[{i}]";
                Transform tChild = tRoot.Find(wantedName);
                if (tChild == null)
                {
                    tChild = MakeNewObject(wantedName, tRoot);
                }

                if (!_viewObjects.Contains(tChild))
                {
                    _viewObjects.Add(tChild);
                }

                ReconstructMass(mass, tChild);
                i++;
            }
        }

        private Transform MakeNewObject(string name, Transform parent)
        {
            Transform newObject = new GameObject(name, typeof(BodyData)).transform;
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



        private abstract class WorldDataState : State<WorldDataEditor>
        {
            protected WorldDataState(WorldDataEditor master) : base(master)
            {
            }

            public abstract void OnSceneGui();
        }

        private interface ICreateChildButtonState<T>
        {
            void OnChildCreated(T created);
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

        private class CreateRootState : WorldDataState, ICreateChildButtonState<IMass>
        {
            public CreateRootState(WorldDataEditor master) : base(master)
            {
            }

            public override void Update()
            {
                if (GUILayout.Button("Create root", GUILayout.Width(DefaultButtonSize)))
                {
                    Master._currentState = new SelectNewChildState(this, PossibleMass.Dual | PossibleMass.Single, Master);
                }
            }

            public override void OnSceneGui()
            {
            }

            public void OnChildCreated(IMass newChild)
            {
                if (newChild == null)
                {
                    Master._currentState = this;
                    return;
                }

                Master._container.Root = newChild;
                Master.ApplyChanges();
                Master._currentState = new ExtendTreeState(Master);
            }
        }

        private class SelectNewChildState : WorldDataState, ICreateChildButtonState<CelestialSettings?>
        {
            private ICreateChildButtonState<IMass> _lastState;
            private PossibleMass _massMask;

            public SelectNewChildState(ICreateChildButtonState<IMass> lastState, PossibleMass mask, WorldDataEditor master) : base(master)
            {
                _lastState = lastState;
                _massMask = mask;
            }

            public override void Update()
            {
                if (_massMask.HasFlag(PossibleMass.Single) && GUILayout.Button("Planet with satellites", GUILayout.Width(DefaultButtonSize)))
                {
                    _lastState.OnChildCreated(new SingleCenterBranch());
                }

                if (_massMask.HasFlag(PossibleMass.Dual) && GUILayout.Button("Dual system", GUILayout.Width(DefaultButtonSize)))
                {
                    _lastState.OnChildCreated(new DoubleSystemBranch());
                }

                if (_massMask.HasFlag(PossibleMass.Celestial) && GUILayout.Button("Celestial", GUILayout.Width(DefaultButtonSize)))
                {
                    Master._currentState = new SetupCelestialSettingsState(this, Master);
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                {
                    _lastState.OnChildCreated(null);
                }
            }

            public override void OnSceneGui()
            {
            }

            public void OnChildCreated(CelestialSettings? created)
            {
                if (created == null)
                {
                    Master._currentState = this;
                    return;
                }

                _lastState.OnChildCreated(new CelestialBody(created.Value));
            }
        }

        private class SetupCelestialSettingsState : WorldDataState
        {
            private ICreateChildButtonState<CelestialSettings?> _lastState;
            private CelestialSettings _settingsA;
            private CelestialSettings _settingsB;

            public SetupCelestialSettingsState(ICreateChildButtonState<CelestialSettings?> lastState,
                WorldDataEditor master) : base(master)
            {
                _lastState = lastState;
            }

            public override void Update()
            {
                GUILayout.Label("Setup:");

                EditorGUI.BeginChangeCheck();
                _settingsA.mass = EditorGUILayout.DelayedFloatField(_settingsB.mass);
                _settingsA.latitudeShift = EditorGUILayout.DelayedFloatField(_settingsB.latitudeShift);
                _settingsA.longitudeShift = EditorGUILayout.DelayedFloatField(_settingsB.longitudeShift);
                _settingsA.pericenterRadius = EditorGUILayout.DelayedFloatField(_settingsB.pericenterRadius);
                _settingsA.pericenterSpeed = EditorGUILayout.DelayedFloatField(_settingsB.pericenterSpeed);
                if (EditorGUI.EndChangeCheck())
                {
                    _settingsB = _settingsA;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ok"))
                {
                    _lastState.OnChildCreated(_settingsB);
                }

                if (GUILayout.Button("Cancel"))
                {
                    _lastState.OnChildCreated(null);
                }

                GUILayout.EndHorizontal();
            }

            public override void OnSceneGui()
            {
            }
        }


        private class ExtendTreeState : WorldDataState, ICreateChildButtonState<IMass>
        {
            private Action<IMass> _addressHandler;
            public ExtendTreeState(WorldDataEditor master) : base(master)
            {
            }

            public override void Update()
            {
                DrawIMass(Master._container.Root);
            }

            private void DrawIMass(IMass mass)
            {
                BeginTab();
                switch (mass)
                {
                    case SingleCenterBranch singleBranch:
                        GUILayout.Box("Planet with satellites:");
                        if (DrawProperty("Central", PossibleMass.Celestial | PossibleMass.Dual, singleBranch.Central))
                        {
                            _addressHandler = v => singleBranch.Central = v;
                        }
                        int i = 0;
                        for (i = 0; i < singleBranch.Children.Count; i++)
                        {
                            if (DrawProperty($"Child[{i}]", PossibleMass.All, singleBranch.Children[i]))
                            {
                                int ii = i;
                                _addressHandler = v => singleBranch.Children[ii] = v;
                            }
                        }
                        
                        if (DrawProperty($"new child", PossibleMass.All, null))
                        {
                            singleBranch.Children.Add(null);
                            int ii = i;
                            _addressHandler = v => singleBranch.Children[ii] = v;
                        }
                        break;
                    case DoubleSystemBranch doubleBranch:
                        GUILayout.Box("Dual system");
                        if (DrawProperty("A", PossibleMass.All, doubleBranch.ChildA))
                        {
                            _addressHandler = v => doubleBranch.ChildA = v;
                        }
                        if (DrawProperty("B", PossibleMass.All, doubleBranch.ChildB))
                        {
                            _addressHandler = v => doubleBranch.ChildB = v;
                        }
                        break;
                    case CelestialBody body:
                        GUILayout.Box("Celestial");
                        break;
                }
                EndTab();
            }

            private bool DrawProperty(string header, PossibleMass mask, IMass value)
            {
                BeginTab();
                if (value == null)
                {
                    if (GUILayout.Button("+ " + header, GUILayout.Width(DefaultButtonSize)))
                    {
                        Master._currentState = new SelectNewChildState(this, mask, Master);
                        return true;
                    }
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(header, GUILayout.Width(DefaultButtonSize)))
                    {
                        Master._currentState = new SelectNewChildState(this, mask, Master);
                        return true;
                    }

                    DrawIMass(value);
                    GUILayout.EndHorizontal();
                }
                EndTab();
                return false;
            }

            public override void OnSceneGui()
            {
            }

            public void OnChildCreated(IMass created)
            {
                _addressHandler.Invoke(created);
                Master.ApplyChanges();
                Master._currentState = this;
            }
        }
    }
#endif
}