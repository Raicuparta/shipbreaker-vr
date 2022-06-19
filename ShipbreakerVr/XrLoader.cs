﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Valve.VR;

namespace ShipbreakerVr;

public class XrLoader: MonoBehaviour
{
	private void Awake()
	{
		// SteamVR_Actions.PreInitialize();
		LoadXRModule();

		SteamVR.Initialize();
		SteamVR.settings.pauseGameWhenDashboardVisible = false;

		// InputTracking.disablePositionalTracking = true;
		
		if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
		                                       && XRGeneralSettings.Instance.Manager.activeLoader != null)
		{
			// XRGeneralSettings.Instance.Manager.StartSubsystems();
		}
		else
			throw new Exception("Cannot initialize VRSubsystem");

		//Change tracking origin to headset
		var subsystems = new List<XRInputSubsystem>();
		SubsystemManager.GetInstances(subsystems);
		foreach (var subsystem in subsystems)
		{
			subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
			subsystem.TryRecenter();
		}
	}

	private void Update()
	{
		// if (Input.GetKeyDown(KeyCode.KeypadMinus))
		// {
		// 	Camera.main.transform.parent.localScale *= 1.1f;
		// }
		// if (Input.GetKeyDown(KeyCode.KeypadPlus))
		// {
		// 	Camera.main.transform.parent.localScale *= 0.9f;
		// }
	}

	private static void LoadXRModule()
	{
		var xrManagerBundle = VrAssetManager.LoadBundle("xrmanager");

		foreach (var xrManager in xrManagerBundle.LoadAllAssets())
			Debug.Log($"######## Loaded xrManager: {xrManager.name}");

		var instance = XRGeneralSettings.Instance;
		if (instance == null) throw new Exception("XRGeneralSettings instance is null");

		var xrManagerSettings = instance.Manager;
		if (xrManagerSettings == null) throw new Exception("XRManagerSettings instance is null");

		xrManagerSettings.InitializeLoaderSync();
		if (xrManagerSettings.activeLoader == null) throw new Exception("Cannot initialize OpenVR Loader");

		var openXrSettings = ScriptableObject.FindObjectOfType<OpenXRSettings>(true);
		
		Debug.Log($"### Found OpenXrSettinghs? {openXrSettings != null}");

		// var openVrSettings = OpenVRSettings.GetSettings(false);
		// openVrSettings.EditorAppKey = "steam.app.753640";
		// openVrSettings.InitializationType = OpenVRSettings.InitializationTypes.Scene;
		// openVrSettings.StereoRenderingMode = OpenVRSettings.StereoRenderingModes.SinglePassInstanced;
		// if (openVrSettings == null) throw new Exception("OpenVRSettings instance is null");
		//
		// openVrSettings.SetMirrorViewMode(OpenVRSettings.MirrorViewModes.Right);
	}
}