using System;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace ShipbreakerVr;

public class ModXrManager : MonoBehaviour
{
    public static bool IsVrEnabled;
    private static OpenXRLoaderBase openXrLoader;
    public static Action<bool> OnVrModeChange;
    private static bool IsInitialized => openXrLoader != null && openXrLoader.GetValue<bool>("isInitialized");

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

        openXrLoader = xrManagerSettings.ActiveLoaderAs<OpenXRLoaderBase>();

        // Reference OpenXRSettings just to make this work.
        // TODO figure out how to do this properly.
        OpenXRSettings unused;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            if (!IsVrEnabled)
            {
                XRGeneralSettings.Instance.Manager.StartSubsystems();
                XRGeneralSettings.Instance.Manager.activeLoader.Initialize();
                XRGeneralSettings.Instance.Manager.activeLoader.Start();
            }
            else
            {
                XRGeneralSettings.Instance.Manager.activeLoader.Stop();
                XRGeneralSettings.Instance.Manager.activeLoader.Deinitialize();
            }

            IsVrEnabled = IsInitialized;

            OnVrModeChange?.Invoke(IsVrEnabled);
        }
    }
}