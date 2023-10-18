#if  UNITY_EDITOR
using System;
using System.Collections.Generic;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class ExtendTreeState : WorldDataState, INestedStateUser<IMassSystem>, INestedStateUser<TrajectorySettings?>, INestedStateUser<DoubleSystemTrajectorySettings?>
        {
            private Action<IMassSystem> _massAddressHandler;
            private DoubleSystemBranch _doubleSettingsAddress;
            private IMassSystem _settingsAddress;
            private IMassSystem _parentCache;
            private IMassSystem _editCache;

            public ExtendTreeState(WorldDataEditor master) : base(master)
            {
            }

            public override void Update()
            {
                DrawIMass(Master._tree.Root);
            }

            private void DrawIMass(IMassSystem massSystem)
            {
                _parentCache = massSystem;

                switch (massSystem)
                {
                    case SingleCenterBranch singleBranch:
                        GUILayout.Box("Planet with satellites:");
                        if (DrawProperty("Central", PossibleMass.Celestial | PossibleMass.Dual, singleBranch.Central))
                        {
                            _massAddressHandler = v => Address(ref singleBranch.Central, v);
                        }

                        int i = 0;
                        for (i = 0; i < singleBranch.Children.Count; i++)
                        {
                            if (DrawProperty($"Child[{i}]", PossibleMass.All, singleBranch.Children[i]))
                            {
                                int ii = i;
                                _massAddressHandler = v => AddressIndex(ref singleBranch.Children, ii, v);
                            }
                        }

                        if (DrawProperty($"new child", PossibleMass.All, null))
                        {
                            singleBranch.Children.Add(null);
                            int ii = i;
                            _massAddressHandler = v => AddressIndex(ref singleBranch.Children, ii, v);
                        }

                        break;
                    case DoubleSystemBranch doubleBranch:
                        GUILayout.Box("Dual system");
                        if (DrawProperty("A", PossibleMass.All, doubleBranch.ChildA))
                        {
                            _massAddressHandler = v => Address(ref doubleBranch.ChildA, v);
                        }

                        if (DrawProperty("B", PossibleMass.All, doubleBranch.ChildB))
                        {
                            _massAddressHandler = v => Address(ref doubleBranch.ChildB, v);
                        }

                        break;
                    case CelestialBody body:
                        //GUILayout.Box("Celestial");
                        break;
                }
            }

            private void Address(ref IMassSystem lastMassSystem, IMassSystem newMassSystem)
            {
                if (lastMassSystem != null)
                {
                    newMassSystem.Settings = lastMassSystem.Settings;
                }
                lastMassSystem = newMassSystem;
            }
            private void AddressIndex(ref List<IMassSystem> lastMass, int index, IMassSystem newMassSystem)
            {
                if (lastMass[index] != null)
                {
                    newMassSystem.Settings = lastMass[index].Settings;
                }

                lastMass[index] = newMassSystem;
            }

            private bool DrawProperty(string header, PossibleMass mask, IMassSystem value)
            {
                IMassSystem parent = _parentCache;
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
                        Master._currentEdit = _editCache;
                        Master._currentParent = _parentCache;
                        if (Master._currentParent is DoubleSystemBranch doubleSystemBranch)
                        {
                            _doubleSettingsAddress = doubleSystemBranch;
                            Master._currentState = new SetupDoubleSettingsState(doubleSystemBranch.LocalTrajectorySettings, this, Master);
                        }
                        else
                        {
                            _settingsAddress = value;
                            Master._currentState = new SetupCelestialSettingsState(value.Settings, this, Master);
                        }
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

            public void NestedStateCallback(IMassSystem value, bool final)
            {
                if (value != null)
                {
                    _massAddressHandler.Invoke(value);
                    Master.ApplyChanges(final);
                }

                if (final)
                {
                    Master._currentState = this;
                }
            }

            public void NestedStateCallback(TrajectorySettings? value, bool final)
            {
                if (value != null)
                {
                    _settingsAddress.Settings = value.Value;
                    Master.ApplyChanges(final);
                }

                if (final)
                {
                    Master._currentState = this;
                }
            }

            public void NestedStateCallback(DoubleSystemTrajectorySettings? value, bool final)
            {
                if (value != null)
                {
                    _doubleSettingsAddress.LocalTrajectorySettings = value.Value;
                    Master.ApplyChanges(final);
                }

                if (final)
                {
                    Master._currentState = this;
                }
            }
        }
    }
}
#endif
