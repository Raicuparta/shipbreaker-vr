﻿using UnityEngine;
using UnityEngine.SpatialTracking;

namespace ShipbreakerVr;

public class VrCamera : MonoBehaviour
{
    private Camera vrCamera;

    public static Camera MainCamera { get; private set; }

    public static void Create(Camera mainCamera)
    {
        var instance = new GameObject("VrCamera").AddComponent<VrCamera>();
        instance.transform.SetParent(mainCamera.transform, false);
        MainCamera = mainCamera;

        instance.vrCamera = instance.gameObject.AddComponent<Camera>();
        instance.vrCamera.farClipPlane = mainCamera.farClipPlane;
        instance.vrCamera.nearClipPlane = mainCamera.nearClipPlane;
        instance.vrCamera.cullingMask = mainCamera.cullingMask;
        instance.vrCamera.transform.localPosition = Vector3.forward * 0.2f;
        instance.vrCamera.tag = "MainCamera";

        var poseDriver = instance.gameObject.AddComponent<TrackedPoseDriver>();
        poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
    }

    private void Update()
    {
        MainCamera.enabled = !ModXrManager.IsVrEnabled;
        vrCamera.enabled = ModXrManager.IsVrEnabled;
    }
}