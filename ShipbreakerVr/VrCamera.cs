﻿using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.Management;

namespace ShipbreakerVr;

public class VrCamera: MonoBehaviour
{
	private Camera mainCamera;
	private Camera vrCamera;
	
	public static void Create(Camera mainCamera)
	{
		var instance = new GameObject("VrCamera").AddComponent<VrCamera>();
		instance.transform.SetParent(mainCamera.transform, false);
		instance.mainCamera = mainCamera;
		
		instance.vrCamera = instance.gameObject.AddComponent<Camera>();
		instance.vrCamera.farClipPlane = mainCamera.farClipPlane;
		instance.vrCamera.nearClipPlane = mainCamera.nearClipPlane;
		instance.vrCamera.cullingMask = mainCamera.cullingMask;
		instance.vrCamera.transform.localPosition = Vector3.forward * 0.2f;
		
		var poseDriver = instance.gameObject.AddComponent<TrackedPoseDriver>();
		poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
	}

	private void Update()
	{
		mainCamera.enabled = !Plugin.IsVrEnabled;
		vrCamera.enabled = Plugin.IsVrEnabled;
	}
}