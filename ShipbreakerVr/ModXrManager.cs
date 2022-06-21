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
            }
            else
            {
                XRGeneralSettings.Instance.Manager.activeLoader.Stop();
                XRGeneralSettings.Instance.Manager.activeLoader.Deinitialize();
            }

            IsVrEnabled = IsInitialized;
            SetUpUi(IsVrEnabled);
        }
    }

    private void SetUpUi(bool isVr)
    {
        var hudSystem = FindObjectOfType<GlassModeController>();
        if (!hudSystem) Debug.LogError("Failed to find HUD System");

        var hudCanvasHelmet = hudSystem.transform.Find("HUDContainer/HUD Canvas - Helmet").GetComponent<Canvas>();
        hudCanvasHelmet.transform.SetParent(Camera.main.transform, false);
        hudCanvasHelmet.transform.localPosition = Vector3.forward * 3;
        hudCanvasHelmet.transform.localScale = Vector3.one * 0.003f;
        hudCanvasHelmet.transform.localRotation = Quaternion.identity;

        hudCanvasHelmet.renderMode = isVr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;

        if (isVr) MaterialHelper.MakeGraphicChildrenDrawOnTop(hudCanvasHelmet.gameObject);

        hudCanvasHelmet.GetComponent<CanvasScalerImprover>().enabled = !isVr;
        hudCanvasHelmet.GetComponent<CanvasScaler>().enabled = !isVr;

        var hudCanvasOther = hudSystem.transform.Find("HUDContainer/HUD Canvas - Other").GetComponent<Canvas>();
        hudCanvasOther.transform.SetParent(Camera.main.transform, false);
        hudCanvasOther.transform.localPosition = Vector3.forward * 3;
        hudCanvasOther.transform.localScale = Vector3.one * 0.004f;
        hudCanvasOther.transform.localRotation = Quaternion.identity;

        hudCanvasOther.renderMode = isVr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;

        if (isVr) MaterialHelper.MakeGraphicChildrenDrawOnTop(hudCanvasOther.gameObject);

        hudCanvasOther.GetComponent<CanvasScalerImprover>().enabled = !isVr;
        hudCanvasOther.GetComponent<CanvasScaler>().enabled = !isVr;
    }
}