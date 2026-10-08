using System;
using System.Collections.Generic;
using BBI.Unity.Game;
using BepInEx.Configuration;
using HarmonyLib;
using InControl;
using ShipbreakerVr.Tracking;
using UnityEngine;

namespace ShipbreakerVr;

// Arbitrate after all native devices commit, before PlayerActions consume them.
// Uses the game's existing InControl pads, including Steam Input's virtual pad.
[HarmonyPatch]
internal sealed class VrInputMode : MonoBehaviour
{
    private static VrInputMode instance;
    private static ConfigEntry<Tracking.VrInputMode> setting;
    private readonly InputModeSelection selection = new InputModeSelection();
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly InputActivity motionActivity = new InputActivity(32);
    private readonly Dictionary<InputDevice, InputActivity> pads = new Dictionary<InputDevice, InputActivity>();
    private readonly List<InputDevice> removed = new List<InputDevice>();
    private InputSystemUpdateBridge bridge;
    private InputDevice selectedPad;
    private bool padArmed, failed, wasEligible;
    private float nextReport;
    private string lastReportState;
    private bool reportActivity, reportFailed;
    private static readonly InputControlType[] controls = {
        InputControlType.LeftStickX, InputControlType.LeftStickY, InputControlType.RightStickX, InputControlType.RightStickY,
        InputControlType.DPadX, InputControlType.DPadY, InputControlType.LeftTrigger, InputControlType.RightTrigger,
        InputControlType.Action1, InputControlType.Action2, InputControlType.Action3, InputControlType.Action4,
        InputControlType.LeftBumper, InputControlType.RightBumper, InputControlType.LeftStickButton, InputControlType.RightStickButton,
        InputControlType.Start, InputControlType.Back, InputControlType.Menu, InputControlType.View
    };
    internal static bool MotionActive => ModXrManager.IsVrEnabled && (!instance || (!instance.failed && instance.selection.Motion));
    internal static bool CouchActive => ModXrManager.IsVrEnabled && !MotionActive;
    internal static void Configure(ConfigFile config) => setting = config.Bind("Input", "Mode", Tracking.VrInputMode.Auto,
        "Auto: use a gamepad button/stick for couch play, or a tracked-controller button/stick for motion play. Release controls after switching. Gamepad and MotionControllers lock the choice; VR/HUD stay enabled.");
    private void Awake()
    {
        instance = this;
        InputManager.OnUpdate += ReportCouchInput;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(InputManager), "UpdateActiveDevice")]
    private static void BeforeActions() { if (instance) instance.Sample(); }

    [HarmonyPostfix, HarmonyPatch(typeof(InputManager), "UpdateActiveDevice")]
    private static void ChooseActiveDevice(ref InputDevice ___activeDevice)
    {
        ___activeDevice = NativeGamepadRouting.Filter(___activeDevice, CouchActive, instance && instance.padArmed);
        if (CouchActive && instance && instance.padArmed && ___activeDevice.IsAttached &&
            ___activeDevice.DeviceClass == InputDeviceClass.Controller && !VrMenuControls.IsVirtualDevice(___activeDevice))
            instance.selectedPad = ___activeDevice;
    }

    private bool ReadMotion(TrackedHand hand, int channel)
    {
        var input = hand.Inputs;
        var valid = tracking.Head.IsValid && hand.Grip.IsValid;
        var pressed = motionActivity.Observe(channel++, input.Stick.x, valid && input.StickAvailable);
        pressed |= motionActivity.Observe(channel++, input.Stick.y, valid && input.StickAvailable);
        pressed |= motionActivity.Observe(channel++, hand.Trigger, valid && hand.Aim.IsValid);
        pressed |= motionActivity.Observe(channel++, input.Primary ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.Secondary ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.StickClick ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.Grip ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.X ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.Y ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.Bumper ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.Menu ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.View ? 1 : 0, valid);
        pressed |= motionActivity.Observe(channel++, input.Dpad.x, valid);
        pressed |= motionActivity.Observe(channel, input.Dpad.y, valid);
        return pressed;
    }

    private void Sample()
    {
        try
        {
            if (!ModXrManager.IsVrEnabled || !Application.isFocused || !LynxControls.Instance || !LynxControls.Instance.IsGameFocused)
            {
                pads.Clear(); motionActivity.Reset(); padArmed = false;
                if (wasEligible) ResetOwners();
                wasEligible = false;
                VrMenuControls.RefreshInputOwner();
                return;
            }
            wasEligible = true;
            InputDevice activated = null;
            var previousPad = selectedPad;
            removed.Clear();
            foreach (var entry in pads) if (!entry.Key.IsAttached) removed.Add(entry.Key);
            foreach (var pad in removed) pads.Remove(pad);
            if (selectedPad != null && !selectedPad.IsAttached) { selectedPad = null; padArmed = false; }
            foreach (var pad in InputManager.Devices)
            {
                if (!pad.IsAttached || pad.Passive || pad.DeviceClass != InputDeviceClass.Controller || VrMenuControls.IsVirtualDevice(pad)) continue;
                if (!pads.TryGetValue(pad, out var activity)) pads.Add(pad, activity = new InputActivity(controls.Length));
                for (var i = 0; i < controls.Length; i++)
                    if (activity.Observe(i, pad.GetControl(controls[i]).Value)) activated = pad;
                if (selectedPad == null) selectedPad = pad;
            }
            var motionPressed = false;
            var motionAvailable = false;
            if (!failed)
            {
                try
                {
                    if (bridge == null) bridge = new InputSystemUpdateBridge();
                    bridge.UpdateIfNeeded(); tracking.Sample();
                    motionAvailable = tracking.Head.IsValid && (tracking.LeftHand.Grip.IsValid || tracking.RightHand.Grip.IsValid);
                    motionPressed = ReadMotion(tracking.LeftHand, 0) | ReadMotion(tracking.RightHand, 16);
                }
                catch (Exception error)
                {
                    failed = true;
                    Debug.LogError("[ShipbreakerVr] Input mode XR sampling failed; native gamepad retained. " + error);
                }
            }
            if (activated != null && !ReferenceEquals(activated, selectedPad)) selectedPad = activated;
            var changed = selection.Sample(failed ? Tracking.VrInputMode.Gamepad : setting.Value,
                motionAvailable, selectedPad != null, motionPressed, activated != null, Time.unscaledTime);
            if (changed)
            {
                padArmed = false;
                previousPad?.StopVibration();
                ResetOwners();
                Debug.Log("[ShipbreakerVr] Input mode: " + (selection.Motion ? "motion controllers" : "native gamepad / couch") + "; release buttons/triggers and center sticks to resume.");
            }
            if (!selection.Motion && selectedPad != null && !padArmed)
            {
                var neutral = true;
                foreach (var control in controls)
                    if (Math.Abs(selectedPad.GetControl(control).Value) > .15f) neutral = false;
                padArmed = neutral;
            }
            VrMenuControls.RefreshInputOwner();
        }
        catch (Exception error)
        {
            // Never let an arbitration failure interrupt InControl's pending tick.
            failed = true; padArmed = false;
            Debug.LogError("[ShipbreakerVr] Input mode handoff failed; native input fallback. " + error);
            // A failure here must not throw out of the native input update.
            try { VrMenuControls.RefreshInputOwner(); } catch (Exception) { }
        }
    }
    // Observe only; never undo the game's intentional interaction/cutscene blockers.
    // Pair raw controls and actions from the same input tick, not a previous press
    // with actions sampled two seconds later. State changes help diagnose a stopped pad without
    // logging every normal gameplay input or adding work to the render path.
    private void ReportCouchInput(ulong tick, float deltaTime)
    {
        if (!VrMenuControls.VerboseInputEnabled || !CouchActive || reportFailed) return;
        try
        {
            var native = InputManager.ActiveDevice;
            reportActivity = false;
            foreach (var control in controls) reportActivity |= native.GetControl(control).WasPressed;
            if (Time.unscaledTime < nextReport) return;
            nextReport = Time.unscaledTime + 2f;
            var state = GameSession.CurrentGameState;
            var type = state == GameSession.GameState.Gameplay ? LynxControls.PlayerActionSetTypes.GameplayActions :
                state == GameSession.GameState.Paused ? LynxControls.PlayerActionSetTypes.PausedActions :
                state == GameSession.GameState.NIS ? LynxControls.PlayerActionSetTypes.NISActions : LynxControls.PlayerActionSetTypes.FEActions;
            var actions = LynxControls.Instance ? LynxControls.Instance.TryGetLoadedActionSet(type) : null;
            var status = $"state={state}; focused={Application.isFocused}/{(LynxControls.Instance && LynxControls.Instance.IsGameFocused)}; armed={padArmed}; active={native.Name}/{native.GetType().Name}; slot={(native is XInputDevice xinput ? xinput.DeviceIndex : -1)}; attached={native.IsAttached}; passive={native.Passive}; set={type}/{actions?.Enabled}; listening={actions?.IsListeningForBinding}; pinned={actions?.Device?.Name ?? "automatic"}";
            var hasAction = false;
            if (actions != null) foreach (var action in actions.Actions) hasAction |= Math.Abs(action.Value) > .01f;
            if (status != lastReportState || (reportActivity && !hasAction))
            {
                var evidence = new System.Text.StringBuilder();
                foreach (var control in controls)
                    if (Math.Abs(native.GetControl(control).Value) > .01f)
                        evidence.Append($" raw.{control}={native.GetControl(control).Value:F2};");
                if (actions != null)
                    foreach (var action in actions.Actions)
                        if (!action.EnabledInHierarchy || Math.Abs(action.Value) > .01f || (reportActivity && !hasAction))
                            evidence.Append($" {action.Name}={action.Value:F2}/{action.EnabledInHierarchy}/bindings={action.Bindings.Count};");
                Debug.Log($"[ShipbreakerVr] Couch input tick={tick}; {status}; buttonActivity={reportActivity}; actionActive={hasAction};{evidence}");
            }
            lastReportState = status; reportActivity = false;
        }
        catch (Exception error)
        {
            reportFailed = true;
            Debug.LogWarning("[ShipbreakerVr] Couch input diagnostics disabled: " + error.Message);
        }
    }
    private static void ResetOwners()
    {
        VrMenuControls.InputOwnerChanged();
        VrMovementControls.InputOwnerChanged();
        VrStingerControls.InputOwnerChanged();
        VrGrappleControls.InputOwnerChanged();
        VrAdditionalToolControls.InputOwnerChanged();
        VrHaptics.InputOwnerChanged();
        VrAvatarVisuals.RestoreEarly();
    }
    private void OnDestroy() { InputManager.OnUpdate -= ReportCouchInput; bridge?.Dispose(); if (instance == this) instance = null; }
}
