using System;
using System.Collections;
using System.Collections.Generic;
using Ara3D;
using Orbital.Core;
using Orbital.Core.KSPSource;
using Sirenix.OdinInspector;
using UnityEngine;

public class Test : MonoBehaviour
{
    [ShowInInspector] private Orbit orbit;
    public GameObject body;
    public double inclination;
    public double eccentricity;
    public double sma;
    public double largeAscendingNode;
    public double argumentPericenter;
    public double meanAnomalyAtEpoch;
    public double t;

    public Vector3 velocity;
    public Vector3 position;
    public double UT;
    
    private void Start()
    {
        CreateFromProperties();
    }

    [Button]
    private void CreateFromProperties()
    {
        orbit = new Orbit(inclination, eccentricity, sma, largeAscendingNode, argumentPericenter, meanAnomalyAtEpoch, t, body.GetComponent<IStaticBody>());
    }
    [Button]
    private void UpdateFromVelocity()
    {
        orbit.UpdateFromStateVectors(position, velocity, body.GetComponent<IStaticBody>(), UT);
    }

    [Button]
    public void GetOrbitalStateVectorsAtTrueAnomaly(double ObT, double ut)
    {
        DVector3 pos, vel;
        Debug.Log(orbit.GetOrbitalStateVectorsAtObT(ObT, ut, out pos, out vel));
        velocity = vel;
        position = pos;
    }


    private void Update()
    {
        orbit.DrawOrbit(Color.red);
    }
}
