using System;
using BBI.Unity.Game;
using BepInEx.Configuration;
using HarmonyLib;
using ShipbreakerVr.Tracking;
using UnityEngine;
using UnityEngine.XR;

namespace ShipbreakerVr;

[DefaultExecutionOrder(11000)]
internal sealed class VrHaptics : MonoBehaviour
{
    private static VrHaptics instance;
    private static ConfigEntry<bool> enabledSetting;
    private static ConfigEntry<float> strength;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly HapticEnvelope left = new HapticEnvelope(), right = new HapticEnvelope();
    private readonly XrHapticChannel leftOutput = new XrHapticChannel(XRNode.LeftHand), rightOutput = new XrHapticChannel(XRNode.RightHand);
    private bool failed, wasGrappling;
    internal static void Configure(ConfigFile config)
    {
        enabledSetting = config.Bind("Haptics", "Enabled", true, "Tool vibration through standard XR controller haptics. Also respects the game's vibration setting.");
        strength = config.Bind("Haptics", "Strength", .5f, new ConfigDescription("Overall VR vibration strength; 0 disables output.", new AcceptableValueRange<float>(0, 1)));
    }
    private bool Eligible => !failed && enabledSetting.Value && strength.Value > 0 && VrInputMode.MotionActive &&
        Application.isFocused && GameSession.CurrentGameState == GameSession.GameState.Gameplay &&
        !EquipmentController.ToolMenuOpen && !VrMenuControls.PauseInputConsumed;
    private void Awake() => instance = this;
    internal static void InputOwnerChanged() { if (instance) instance.Stop(); }
    internal static void Pulse(bool leftHand, float amplitude, float duration)
    {
        if (instance && instance.Eligible) (leftHand ? instance.left : instance.right).Queue(amplitude, duration, Time.unscaledTime);
    }
    private void LateUpdate()
    {
        try
        {
            if (!Eligible) { Stop(); return; }
            tracking.Sample();
            if (!tracking.Head.IsValid) { Stop(); return; }
            var leftLevel = 0f; var rightLevel = 0f;
            var equipment = VrToolPresentation.Equipment;
            var grapple = VrToolPresentation.Grapple;
            var grabbing = equipment && equipment.CurrentEquipment == EquipmentController.Equipment.GrappleHook && grapple && grapple.GrappledRigidbody;
            if (grabbing)
            {
                if (!wasGrappling) right.Queue(.6f, .08f, Time.unscaledTime);
                rightLevel = .18f;
                if (grapple.IsRetracting) leftLevel = .35f;
            }
            wasGrappling = grabbing;
            var cutter = VrToolPresentation.Cutter;
            if (equipment && equipment.CurrentEquipment == EquipmentController.Equipment.CuttingTool && cutter &&
                cutter.CurrentMode == CuttingToolController.CutterMode.Scalpel && cutter.State == CuttingState.Cutting)
                rightLevel = VrToolPresentation.Stinger && VrToolPresentation.Stinger.Target.IsValid ? .4f : .12f;
            var gain = strength.Value * (LynxControls.Instance ? LynxControls.Instance.GetVibrationIntensity(1f) : 1f);
            if (gain <= 0) { Stop(); return; }
            Drive(left, leftOutput, tracking.LeftHand.Grip.IsValid, leftLevel, gain);
            Drive(right, rightOutput, tracking.RightHand.Grip.IsValid, rightLevel, gain);
        }
        catch (Exception error)
        {
            failed = true; Stop(); Debug.LogWarning("[ShipbreakerVr] Haptics stopped; controls unaffected. " + error);
        }
    }
    private static void Drive(HapticEnvelope envelope, XrHapticChannel output, bool tracked, float sustained, float gain)
    {
        if (!tracked) { envelope.Clear(); output.Stop(); return; }
        if (envelope.Sample(Time.unscaledTime, sustained, out var amplitude)) output.Send(amplitude * gain);
    }
    private void Stop()
    {
        left.Clear(); right.Clear(); wasGrappling = false;
        leftOutput.Stop(); rightOutput.Stop();
    }
    private void OnDisable() => Stop();
    private void OnDestroy() { Stop(); if (instance == this) instance = null; }
}

[HarmonyPatch]
internal static class HapticEventPatches
{
    internal static void InstallOptional()
    {
        var harmony = new Harmony("ShipbreakerVr.Haptics");
        try { harmony.CreateClassProcessor(typeof(HapticEventPatches)).Patch(); }
        catch (Exception error) { harmony.UnpatchSelf(); Debug.LogWarning("[ShipbreakerVr] Haptic event hooks unavailable; continuous tool haptics retained. " + error); }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(CuttingFXController), "OnCuttingChangedEvent")]
    private static void Cut(CuttingChangedEvent ev)
    {
        if (ev.State == CuttingChangedEvent.CutState.Cutting) VrHaptics.Pulse(false, .75f, .12f);
        else if (ev.State == CuttingChangedEvent.CutState.Missed) VrHaptics.Pulse(false, .2f, .05f);
    }
    [HarmonyPostfix, HarmonyPatch(typeof(TetherController), "SpawnFireFX")]
    private static void Tether() => VrHaptics.Pulse(true, .5f, .08f);
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeController), "SetState")]
    private static void Demo(DemoChargeState newState)
    {
        if (newState == DemoChargeState.Placing || newState == DemoChargeState.Throwing) VrHaptics.Pulse(false, .45f, .07f);
        else if (newState == DemoChargeState.Detonating) VrHaptics.Pulse(false, .8f, .16f);
    }
}
