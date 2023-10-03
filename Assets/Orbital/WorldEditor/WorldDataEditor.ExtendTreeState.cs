using System;
using System.Collections.Generic;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEngine;

namespace Orbital.WorldEditor
{
    public partial class WorldDataEditor
    {
        private class ExtendTreeState : WorldDataState, INestedStateUser<IMass>, INestedStateUser<CelestialSettings?>, INestedStateUser<DoubleSystemSettings?>
        {
            private Action<IMass> _massAddressHandler;
            private DoubleSystemBranch _doubleSettingsAddress;
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

            private void Address(ref IMass lastMass, IMass newMass)
            {
                if (lastMass != null)
                {
                    newMass.Settings = lastMass.Settings;
                }
                lastMass = newMass;
            }
            private void AddressIndex(ref List<IMass> lastMass, int index, IMass newMass)
            {
                if (lastMass[index] != null)
                {
                    newMass.Settings = lastMass[index].Settings;
                }

                lastMass[index] = newMass;
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
                        Master._currentEdit = _editCache;
                        Master._currentParent = _parentCache;
                        if (Master._currentParent is DoubleSystemBranch doubleSystemBranch)
                        {
                            _doubleSettingsAddress = doubleSystemBranch;
                            Master._currentState = new SetupDoubleSettingsState(doubleSystemBranch.LocalSettings, this, Master);
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

            public void NestedStateCallback(IMass value, bool final)
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

            public void NestedStateCallback(CelestialSettings? value, bool final)
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

            public void NestedStateCallback(DoubleSystemSettings? value, bool final)
            {
                if (value != null)
                {
                    _doubleSettingsAddress.LocalSettings = value.Value;
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