using System;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace ShipbreakerVr;

public class XrLoader: MonoBehaviour
{
	private void Awake()
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

		// Reference OpenXRSettings just to make this work.
		// TODO figure out how to do this properly.
		OpenXRSettings unused;
	}
}