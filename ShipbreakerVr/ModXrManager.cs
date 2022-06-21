using System;
using BBI.Unity.Game;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace ShipbreakerVr;

public class ModXrManager : MonoBehaviour
{
    public static bool IsVrEnabled;
    private static OpenXRLoaderBase openXrLoader;
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
                SetUpUi(true);
            }
            else
            {
                XRGeneralSettings.Instance.Manager.activeLoader.Stop();
                XRGeneralSettings.Instance.Manager.activeLoader.Deinitialize();
                SetUpUi(false);
            }

            IsVrEnabled = IsInitialized;
        }
    }

    private void SetUpUi(bool isVr)
    {
        var hudSystem = FindObjectOfType<GlassModeController>();
        if (!hudSystem) Debug.LogError("Failed to find HUD System");
        hudSystem.transform.SetParent(Camera.main.transform, false);
        hudSystem.transform.localPosition = Vector3.forward * 3;
        hudSystem.transform.localScale = Vector3.one * 0.004f;

        var hudCanvasOther = hudSystem.transform.Find("HUDContainer/HUD Canvas - Other/").GetComponent<Canvas>();
        hudCanvasOther.renderMode = isVr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
        hudCanvasOther.transform.localPosition = Vector3.zero;
        hudCanvasOther.transform.localRotation = Quaternion.identity;
        hudCanvasOther.transform.localScale = Vector3.one;

        hudCanvasOther.GetComponent<CanvasScalerImprover>().enabled = !isVr;
        hudCanvasOther.GetComponent<CanvasScaler>().enabled = !isVr;
    }
}