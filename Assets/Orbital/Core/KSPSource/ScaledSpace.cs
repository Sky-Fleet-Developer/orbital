// Decompiled with JetBrains decompiler
// Type: ScaledSpace
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 4B449F28-41F8-4227-ADFA-AD3149C8FDBA
// Assembly location: H:\Games\Kerbal Space Program v1.12.5\KSP_x64_Data\Managed\Assembly-CSharp.dll

using System;
using System.Collections.Generic;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

public class ScaledSpace : MonoBehaviour
{
    public float scaleFactor = 6000f;
    public Transform originTarget;
    //private List<MapObject> scaledSpaceObjects = new List<MapObject>();
    private static DVector3 totalOffset;

    public static ScaledSpace Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            if (Instance != this)
            {
                Destroy(this);
            }
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (!(Instance != null))
            return;

        if (Instance == this) Instance = null;
    }

    public static float ScaleFactor
    {
        get
        {
            if ((bool) (UnityEngine.Object) Instance)
                return Instance.scaleFactor;


            return 1f;
        }
    }

    public static float InverseScaleFactor
    {
        get
        {
            if ((bool) (UnityEngine.Object) Instance)
                return 1f / Instance.scaleFactor;


            return 1f;
        }
    }

    public static Transform SceneTransform
    {
        get
        {
            if ((bool) (UnityEngine.Object) Instance)
                return Instance.transform;

            return null;
        }
    }

    private void Start()
    {
        int num = (bool) (UnityEngine.Object) originTarget ? 1 : 0;
    }

    private void LateUpdate()
    {
       /* if ((bool) (UnityEngine.Object) originTarget)
        {
            DVector3 position = originTarget.position;
            totalOffset += position;
            int count = scaledSpaceObjects.Count;
            while (count-- > 0)
            {
                if ((UnityEngine.Object) scaledSpaceObjects[count] == null)
                {
                    scaledSpaceObjects.RemoveAt(count);
                }
                else
                {
                    Transform transform = scaledSpaceObjects[count].transform;
                    transform.position = transform.position - (Vector3) position;
                }
            }
        }*/
    }

    /*public static void AddScaledSpaceObject(MapObject t)
    {
        if (Instance == null)
            return;

        if (Instance.scaledSpaceObjects == null)
        {
            return;
        }
        else if (Instance.scaledSpaceObjects.Contains(t))
        {
            Debug.LogWarning("Warning, MapObject " + t.name + " already exists in scaled space",
                (UnityEngine.Object) t);
        }
        else
        {
            Instance.scaledSpaceObjects.Add(t);
        }
    }*/

    /* public static void RemoveScaledSpaceObject(MapObject t)
     {
         if (Instance == null)
             return;
         
         if (Instance.scaledSpaceObjects == null)
         {
             return;
         }
         else
         {
             Instance.scaledSpaceObjects.Remove(t);
         }
     }*/

    public static DVector3 LocalToScaledSpace(DVector3 localSpacePoint) =>
        localSpacePoint * InverseScaleFactor - totalOffset;

    public static void LocalToScaledSpace(ref DVector3 localSpacePoint) =>
        localSpacePoint = localSpacePoint * InverseScaleFactor - totalOffset;

    public static void LocalToScaledSpace(List<Vector3> points)
    {
        int count = points.Count;
        while (count-- > 0)
            points[count] = points[count] * InverseScaleFactor - (Vector3) totalOffset;
    }

    public static void LocalToScaledSpace(DVector3[] localSpacePoint, List<Vector3> scaledSpacePoint)
    {
        int length = localSpacePoint.Length;
        double inverseScaleFactor = InverseScaleFactor;
        for (int index = 0; index < length; ++index)
            scaledSpacePoint[index] = localSpacePoint[index] * inverseScaleFactor - totalOffset;
    }

    public static DVector3 ScaledToLocalSpace(DVector3 scaledSpacePoint) =>
        (scaledSpacePoint + totalOffset) * ScaleFactor;

    /*public static void ToggleAll(bool toggleValue)
    {
        for (int index = 0; index < Instance.scaledSpaceObjects.Count; ++index)
            Instance.scaledSpaceObjects[index].gameObject.SetActive(toggleValue);
    }*/

    /*public static void Toggle(CelestialBody celestialBody, bool toggleValue)
    {
        GameObject gameObject = null;
        for (int index = 0; index < Instance.scaledSpaceObjects.Count; ++index)
        {
            if ((UnityEngine.Object) Instance.scaledSpaceObjects[index].celestialBody == (UnityEngine.Object) celestialBody)
            {
                gameObject = Instance.scaledSpaceObjects[index].gameObject;
            }
        }


        if (!(gameObject != null))
            return;

        gameObject.SetActive(toggleValue);
    }*/
}