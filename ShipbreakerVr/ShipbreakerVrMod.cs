using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[BepInPlugin("ShipbreakerVr", "ShipbreakerVr", "0.4.44")]
public class ShipbreakerVrMod : BaseUnityPlugin
{
    internal static ConfigEntry<float> DebugRayLength;
    internal static ConfigEntry<bool> StingerMotionControls;
    private bool enableLights = true;
    private List<Light> lights;
    private VrPerformanceCapture performanceCapture;

    private void Awake()
    {
        Debug.Log($"Loaded ShipbreakerVr. Game version: {Application.version}");
        DebugRayLength = Config.Bind("Controllers", "RayLengthMetres", 2f,
            new ConfigDescription("Length of each visual aim ray in metres.", new AcceptableValueRange<float>(0.1f, 10f)));
        StingerMotionControls = Config.Bind("Controllers", "StingerMotionControls", true,
            "Right-hand aim/trigger for the Stinger. Release trigger after equipping or recovering tracking. Grapple and other tool adapters have separate settings.");

        VrInputMode.Configure(Config);
        VrMovementControls.Configure(Config);
        VrMenuControls.Configure(Config);
        VrGrappleControls.Configure(Config);
        VrAdditionalToolControls.Configure(Config);
        ModXrManager.Configure(Config);
        VrCamera.Configure(Config);
        VrAvatarVisuals.Configure(Config);
        VrToolPresentation.Configure(Config);
        VrHudCurve.Configure(Config);
        VrRoomMarkers.Configure(Config);
        VrHaptics.Configure(Config);
        Debug.Log("[ShipbreakerVr] Legacy joystick bounds protection installed=" + LegacyJoystickSafety.Install());
        var corePatches = new Harmony("ShipbreakerVr.Core");
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            if (type != typeof(HudMeshCapturePatches) && type != typeof(RoomMarkerPatches) && type != typeof(HapticEventPatches) && type != typeof(HelmetDamagePatches) && type != typeof(LegacyJoystickSafety)) corePatches.CreateClassProcessor(type).Patch();

        gameObject.AddComponent<ModXrManager>();
        gameObject.AddComponent<VrInputMode>();
        gameObject.AddComponent<VrCameraSubmission>();
        gameObject.AddComponent<ControllerDebugRays>();
        gameObject.AddComponent<VrStingerControls>();
        gameObject.AddComponent<VrMovementControls>();
        gameObject.AddComponent<VrMenuControls>();
        gameObject.AddComponent<VrGrappleControls>();
        gameObject.AddComponent<VrAdditionalToolControls>();
        gameObject.AddComponent<VrAvatarVisuals>();
        gameObject.AddComponent<VrHelmetDamage>();
        gameObject.AddComponent<VrRoomMarkers>();
        gameObject.AddComponent<VrHaptics>();
        performanceCapture = new VrPerformanceCapture(Config);
        Debug.Log("[ShipbreakerVr] Core VR and controller components initialized.");
        HudMeshCapturePatches.InstallOptional();
        RoomMarkerPatches.InstallOptional();
        HapticEventPatches.InstallOptional();
        HelmetDamagePatches.InstallOptional();
    }

    private void Update()
    {
        performanceCapture?.Tick();
        if (Input.GetKeyDown(KeyCode.F7)) VrUiDiagnostics.Capture();
        if (Input.GetKeyDown(KeyCode.F8)) VrHudCurve.ToggleFollowHead();
        if (Input.GetKeyDown(KeyCode.F5)) SetPotatoQuality();
        if (Input.GetKeyDown(KeyCode.F6)) DisableLights();
    }

    private void OnDisable() => performanceCapture?.Stop();

    private static void SetPotatoQuality()
    {
        QualitySettings.SetQualityLevel(0);
    }

    private void DisableLights()
    {
        enableLights = !enableLights;

        if (!enableLights)
        {
            lights = new List<Light>();
            foreach (var light in FindObjectsOfType<Light>())
            {
                if (light.enabled) lights.Add(light);
                light.enabled = false;
            }
        }
        else
        {
            foreach (var light in lights) light.enabled = true;
        }
    }
}
