using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using ShipbreakerVr.Tracking;
using BepInEx.Configuration;

namespace ShipbreakerVr;

[DefaultExecutionOrder(-10000)]
public class ModXrManager : MonoBehaviour
{
    public static bool IsVrEnabled { get; private set; }
    public static bool IsSessionRunning { get; private set; }
    public static bool ToggleInputConsumed { get; private set; }
    private static ConfigEntry<bool> startInVr;
    private XRManagerSettings manager;
    private bool requested;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly VrToggleGesture gesture = new VrToggleGesture();
    private InputSystemUpdateBridge bridge;
    private bool inputFailed;

    internal static void Configure(ConfigFile config) => startInVr = config.Bind("VR", "StartInVr", true,
        "Start VR automatically when OpenXR is available. F3 or both grips + both centered stick clicks held for 2 seconds switches view; runtime stays connected for controller toggling.");
    private void Start() { if (startInVr.Value) ToggleXr(); }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3)) ToggleXr();
        var display = manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
        IsSessionRunning = display != null && display.running;
        if (!inputFailed)
        {
            try
            {
                var eligible = IsSessionRunning && Application.isFocused;
                if (eligible)
                {
                    if (bridge == null) bridge = new InputSystemUpdateBridge();
                    bridge.UpdateIfNeeded(); tracking.Sample();
                    eligible = tracking.Head.IsValid && tracking.LeftHand.Grip.IsValid && tracking.RightHand.Grip.IsValid;
                }
                else tracking.Clear();
                if (gesture.Sample(eligible, tracking.LeftHand.Inputs, tracking.RightHand.Inputs, Time.unscaledTime)) ToggleXr();
                ToggleInputConsumed = gesture.ConsumesButtons;
            }
            catch (Exception error)
            {
                inputFailed = true; ToggleInputConsumed = false;
                Debug.LogError("[ShipbreakerVr] Controller VR toggle stopped; F3 remains available. " + error);
            }
        }
        IsVrEnabled = requested && IsSessionRunning;
        if (IsVrEnabled) VrCamera.EnsureEarlyRig();
    }

    private void ToggleXr()
    {
        if (manager?.activeLoader != null)
        {
            requested = !requested;
            Debug.Log("[ShipbreakerVr] View switched to " + (requested ? "VR" : "desktop") + "; OpenXR kept running for controller toggle.");
            return;
        }

        try
        {
            SetUpXr();
            manager.InitializeLoaderSync();
            if (!(manager.activeLoader is OpenXRLoaderBase))
                throw new InvalidOperationException("OpenXR loader could not initialize. Check the active PC OpenXR runtime and headset connection.");
            requested = true;
            manager.StartSubsystems();
            Debug.Log("[ShipbreakerVr] OpenXR start requested; waiting for the display subsystem.");
        }
        catch (Exception exception)
        {
            StopXr();
            Debug.LogError($"[ShipbreakerVr] OpenXR startup failed. F3 retries after the runtime is available. {exception}");
        }
    }

    private void SetUpXr()
    {
        if (manager != null) return;
        VrAssetManager.LoadBundle("xrmanager").LoadAllAssets();
        manager = XRGeneralSettings.Instance?.Manager;
        if (manager == null) throw new InvalidOperationException("XRGeneralSettings/manager missing from xrmanager bundle.");
        OpenXrProfiles.EnableStandardControllers();
    }

    private void StopXr()
    {
        requested = false;
        IsVrEnabled = false;
        IsSessionRunning = false;
        ToggleInputConsumed = false;
        if (manager == null) return;
        try { manager.StopSubsystems(); }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            try { manager.DeinitializeLoader(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void OnDestroy() { bridge?.Dispose(); tracking.Clear(); StopXr(); }
}
