using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace ShipbreakerVr.Tracking;

// Backport only the interaction profile to the pinned Unity OpenXR package.
// Game adapters consume FullGamepad semantics, never controller manufacturer names.
public sealed class FrameControllerProfile : OpenXRInteractionFeature
{
    public const string Extension = "XR_VALVE_frame_controller_interaction";
    public const string Profile = "/interaction_profiles/valve/frame_controller_valve";
    private const string Product = "Valve Frame Controller OpenXR";

    [InputControlLayout(displayName = Product, commonUsages = new[] { "LeftHand", "RightHand" })]
    public class FrameController : OculusTouchControllerProfile.OculusTouchController
    {
        [InputControl] public ButtonControl gamepadX { get; private set; }
        [InputControl] public ButtonControl gamepadY { get; private set; }
        [InputControl] public ButtonControl gamepadBumper { get; private set; }
        [InputControl] public ButtonControl gamepadMenu { get; private set; }
        [InputControl] public ButtonControl gamepadView { get; private set; }
        [InputControl] public ButtonControl dpadUp { get; private set; }
        [InputControl] public ButtonControl dpadDown { get; private set; }
        [InputControl] public ButtonControl dpadLeft { get; private set; }
        [InputControl] public ButtonControl dpadRight { get; private set; }
        protected override void FinishSetup()
        {
            base.FinishSetup();
            gamepadX = GetChildControl<ButtonControl>("gamepadX"); gamepadY = GetChildControl<ButtonControl>("gamepadY");
            gamepadBumper = GetChildControl<ButtonControl>("gamepadBumper");
            gamepadMenu = GetChildControl<ButtonControl>("gamepadMenu"); gamepadView = GetChildControl<ButtonControl>("gamepadView");
            dpadUp = GetChildControl<ButtonControl>("dpadUp"); dpadDown = GetChildControl<ButtonControl>("dpadDown");
            dpadLeft = GetChildControl<ButtonControl>("dpadLeft"); dpadRight = GetChildControl<ButtonControl>("dpadRight");
        }
    }

    public static void Install()
    {
        var settings = OpenXRSettings.Instance;
        if (settings.GetFeature<FrameControllerProfile>() != null) return;
        // These fields are serialized by Unity's build pipeline, absent for a mod-created feature.
        // Resolve all fields first; failure leaves the existing settings untouched.
        var fields = new[] { "nameUi", "version", "featureIdInternal", "openxrExtensionStrings" };
        var values = new[] { "Valve Frame Controller", "1.0.0", "shipbreakervr.input.frame", Extension };
        var metadata = new System.Reflection.FieldInfo[fields.Length];
        for (var i = 0; i < fields.Length; i++) metadata[i] = AccessTools.Field(typeof(OpenXRFeature), fields[i]) ?? throw new MissingFieldException(fields[i]);
        var featureField = AccessTools.Field(typeof(OpenXRSettings), "features") ?? throw new MissingFieldException("OpenXRSettings.features");
        var feature = CreateInstance<FrameControllerProfile>();
        for (var i = 0; i < fields.Length; i++) metadata[i].SetValue(feature, values[i]);
        feature.enabled = true;
        var features = new List<OpenXRFeature>(settings.GetFeatures()) { feature };
        featureField.SetValue(settings, features.ToArray());
        Debug.Log("[ShipbreakerVr] Native Frame interaction profile registered (optional runtime extension).");
    }

    protected override bool OnInstanceCreate(ulong instance)
    {
        if (!OpenXRRuntime.IsExtensionEnabled(Extension))
        {
            Debug.Log("[ShipbreakerVr] Frame extension unavailable; existing controller profiles retained.");
            return false;
        }
        return base.OnInstanceCreate(instance);
    }

    protected override void RegisterDeviceLayout() => InputSystem.RegisterLayout<FrameController>(matches:
        new InputDeviceMatcher().WithInterface(XRUtilities.InterfaceMatchAnyVersion).WithProduct(Product));
    protected override void UnregisterDeviceLayout() => InputSystem.RemoveLayout(nameof(FrameController));

    private static ActionConfig Bind(string name, ActionType type, string path, string usage, string hand = null) => new ActionConfig
    {
        name = name, localizedName = name, type = type,
        usages = string.IsNullOrEmpty(usage) ? new List<string>() : new List<string> { usage },
        bindings = new List<ActionBinding> { new ActionBinding { interactionProfileName = Profile, interactionPath = path,
            userPaths = hand == null ? null : new List<string> { hand } } }
    };

    protected override void RegisterActionMapsWithRuntime()
    {
        var common = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.Controller;
        AddActionMap(new ActionMapConfig
        {
            name = "shipbreakerframecontroller", localizedName = Product, desiredInteractionProfile = Profile, manufacturer = "Valve", serialNumber = "",
            deviceInfos = new List<DeviceConfig> {
                new DeviceConfig { characteristics = common | InputDeviceCharacteristics.Left, userPath = UserPaths.leftHand },
                new DeviceConfig { characteristics = common | InputDeviceCharacteristics.Right, userPath = UserPaths.rightHand } },
            actions = new List<ActionConfig> {
                Bind("thumbstick", ActionType.Axis2D, "/input/thumbstick", "Primary2DAxis"),
                Bind("thumbstickClicked", ActionType.Binary, "/input/thumbstick/click", "Primary2DAxisClick"),
                Bind("trigger", ActionType.Axis1D, "/input/trigger/value", "Trigger"),
                Bind("triggerPressed", ActionType.Binary, "/input/trigger/click", "TriggerButton"),
                Bind("grip", ActionType.Axis1D, "/input/squeeze/value", "Grip"),
                Bind("gripPressed", ActionType.Binary, "/input/squeeze/click", "GripButton"),
                Bind("primaryButton", ActionType.Binary, "/input/a/click", "PrimaryButton", UserPaths.rightHand),
                Bind("secondaryButton", ActionType.Binary, "/input/b/click", "SecondaryButton", UserPaths.rightHand),
                Bind("gamepadX", ActionType.Binary, "/input/x/click", "GamepadX", UserPaths.rightHand),
                Bind("gamepadY", ActionType.Binary, "/input/y/click", "GamepadY", UserPaths.rightHand),
                Bind("gamepadBumper", ActionType.Binary, "/input/bumper/click", "GamepadBumper"),
                Bind("gamepadMenu", ActionType.Binary, "/input/menu/click", "GamepadMenu", UserPaths.rightHand),
                Bind("gamepadView", ActionType.Binary, "/input/view/click", "GamepadView", UserPaths.leftHand),
                Bind("dpadUp", ActionType.Binary, "/input/dpad_up/click", "GamepadDpadUp", UserPaths.leftHand),
                Bind("dpadDown", ActionType.Binary, "/input/dpad_down/click", "GamepadDpadDown", UserPaths.leftHand),
                Bind("dpadLeft", ActionType.Binary, "/input/dpad_left/click", "GamepadDpadLeft", UserPaths.leftHand),
                Bind("dpadRight", ActionType.Binary, "/input/dpad_right/click", "GamepadDpadRight", UserPaths.leftHand),
                Bind("devicePose", ActionType.Pose, "/input/grip/pose", "Device"),
                Bind("pointer", ActionType.Pose, "/input/aim/pose", "Pointer"),
                Bind("vibrate", ActionType.Vibrate, "/output/haptic", null)
            }
        });
        Debug.Log("[ShipbreakerVr] Frame native actions bound: ABXY, D-pad, bumpers, Menu/View, sticks, triggers, grip/aim poses and haptics.");
    }
}
