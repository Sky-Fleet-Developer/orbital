using System;
using Orbital.Model.Components;
using Orbital.Model.TrajectorySystem;
using Orbital.WorldEditor.SystemData;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class ExtendTreeState : WorldDataState, INestedStateUser<IMass>, INestedStateUser<CelestialSettings?>
        {
            private Action<IMass> _massAddressHandler;
            private IMass _settingsAddress;
            private IMass _parentCache;
            private IMass _editCache;

            public ExtendTreeState(WorldDataEditor master) : base(master)
            {
            }

            public override void Update()
            {
                DrawIMass(Master._container.Root);
            }

            private void DrawIMass(IMass mass)
            {
                _parentCache = mass;

                switch (mass)
                {
                    case SingleCenterBranch singleBranch:
                        GUILayout.Box("Planet with satellites:");
                        if (DrawProperty("Central", PossibleMass.Celestial | PossibleMass.Dual, singleBranch.Central))
                        {
                            _massAddressHandler = v => singleBranch.Central = v;
                        }

                        int i = 0;
                        for (i = 0; i < singleBranch.Children.Count; i++)
                        {
                            if (DrawProperty($"Child[{i}]", PossibleMass.All, singleBranch.Children[i]))
                            {
                                int ii = i;
                                _massAddressHandler = v => singleBranch.Children[ii] = v;
                            }
                        }

                        if (DrawProperty($"new child", PossibleMass.All, null))
                        {
                            singleBranch.Children.Add(null);
                            int ii = i;
                            _massAddressHandler = v => singleBranch.Children[ii] = v;
                        }

                        break;
                    case DoubleSystemBranch doubleBranch:
                        GUILayout.Box("Dual system");
                        if (DrawProperty("A", PossibleMass.All, doubleBranch.ChildA))
                        {
                            _massAddressHandler = v => doubleBranch.ChildA = v;
                        }

                        if (DrawProperty("B", PossibleMass.All, doubleBranch.ChildB))
                        {
                            _massAddressHandler = v => doubleBranch.ChildB = v;
                        }

                        break;
                    case CelestialBody body:
                        //GUILayout.Box("Celestial");
                        break;
                }
            }

            private bool DrawProperty(string header, PossibleMass mask, IMass value)
            {
                IMass parent = _parentCache;
                _editCache = value;

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

                    if (GUILayout.Button("Edit", GUILayout.Width(DefaultButtonSize)))
                    {
                        _settingsAddress = value;
                        Master._currentEdit = _editCache;
                        Master._currentParent = _parentCache;
                        Master._currentState = new SetupCelestialSettingsState(value.Settings, this, Master);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    BeginTab();
                    DrawIMass(value);
                    EndTab();
                    GUILayout.EndHorizontal();
                }

                _parentCache = parent;
                return false;
            }

            public override void OnSceneGui()
            {
            }

            public void NestedStateCallback(IMass value)
            {
                _massAddressHandler.Invoke(value);
                Master.ApplyChanges();
                Master._currentState = this;
            }

            public void NestedStateCallback(CelestialSettings? value)
            {
                if (value != null)
                {
                    _settingsAddress.Settings = value.Value;
                    Master.ApplyChanges();
                }

                Master._currentState = this;
            }
        }
    }
}