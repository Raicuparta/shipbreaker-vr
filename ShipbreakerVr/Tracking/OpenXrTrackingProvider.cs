using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Input;

namespace ShipbreakerVr.Tracking;

/// <summary>Reads the device actions registered by Unity's OpenXR interaction profiles.</summary>
public sealed class OpenXrTrackingProvider : IVrTrackingProvider
{
    private readonly List<UnityEngine.XR.InputDevice> xrDevices = new List<UnityEngine.XR.InputDevice>();
    private readonly NativeXrHandReader nativeLeft = new NativeXrHandReader(XRNode.LeftHand);
    private readonly NativeXrHandReader nativeRight = new NativeXrHandReader(XRNode.RightHand);
    public TrackedPose Head { get; private set; }
    public TrackedHand LeftHand { get; private set; }
    public TrackedHand RightHand { get; private set; }

    public void Sample()
    {
        // Resolve every sample: no stale device references after reconnect/session restart.
        Head = ReadHead();
        LeftHand = XRController.leftHand != null ? ReadHand(XRController.leftHand) : nativeLeft.Read();
        RightHand = XRController.rightHand != null ? ReadHand(XRController.rightHand) : nativeRight.Read();
    }

    public void Clear()
    {
        Head = default;
        LeftHand = default;
        RightHand = default;
        nativeLeft.Clear();
        nativeRight.Clear();
    }

    public string DescribeDevices()
    {
        var description = new StringBuilder();
        description.Append($"head={Head.IsValid}; left={DescribeHand(XRController.leftHand)}; right={DescribeHand(XRController.rightHand)}; InputSystem devices=");
        foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
            description.Append($"[{device.displayName}, layout={device.layout}, enabled={device.enabled}, usages={string.Join(",", device.usages)}]");
        InputDevices.GetDevices(xrDevices);
        description.Append("; XR devices=");
        foreach (var device in xrDevices)
            description.Append($"[{device.name}, {device.characteristics}, valid={device.isValid}]");
        description.Append($"; nativeLeft={nativeLeft.Description}, aimValid={LeftHand.Aim.IsValid}; nativeRight={nativeRight.Description}, aimValid={RightHand.Aim.IsValid}");
        return description.ToString();
    }

    private static string DescribeHand(XRController controller)
    {
        if (controller == null) return "no device";
        var pointer = controller.TryGetChildControl<PoseControl>("pointer");
        if (pointer == null) return $"{controller.layout}: no aim control";
        var pose = pointer.ReadValue();
        return $"{controller.layout}: enabled={controller.enabled}, tracked={pose.isTracked}, state={pose.trackingState}";
    }

    private static TrackedPose ReadHead()
    {
        var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) ||
            !device.TryGetFeatureValue(CommonUsages.trackingState, out var state)) return default;

        // Match the legacy TrackedPoseDriver's Center eye source.
        if (!device.TryGetFeatureValue(CommonUsages.centerEyePosition, out var position) ||
            !device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out var rotation)) return default;
        return TrackedPose.FromSample(tracked, state, position, rotation);
    }

    private static TrackedHand ReadHand(XRController controller)
    {
        if (controller == null || !controller.added || !controller.enabled) return default;
        var grip = ReadPose(controller.TryGetChildControl<PoseControl>("devicePose"));
        var aim = ReadPose(controller.TryGetChildControl<PoseControl>("pointer"));
        var trigger = controller.TryGetChildControl<AxisControl>("trigger");
        var stick = controller.TryGetChildControl<Vector2Control>("thumbstick") ?? controller.TryGetChildControl<Vector2Control>("primary2DAxis");
        var primary = controller.TryGetChildControl<ButtonControl>("primaryButton");
        var secondary = controller.TryGetChildControl<ButtonControl>("secondaryButton");
        var stickClick = controller.TryGetChildControl<ButtonControl>("thumbstickClicked") ?? controller.TryGetChildControl<ButtonControl>("primary2DAxisClick");
        var gripButton = controller.TryGetChildControl<ButtonControl>("gripPressed");
        bool Button(string name) => controller.TryGetChildControl<ButtonControl>(name)?.isPressed == true;
        var fullGamepad = controller is FrameControllerProfile.FrameController;
        if (!fullGamepad)
            return new TrackedHand(grip, aim, aim.IsValid && trigger != null ? Mathf.Clamp01(trigger.ReadValue()) : 0f,
                new ControllerInputs(stick != null, stick != null ? stick.ReadValue() : Vector2.zero,
                    primary != null && primary.isPressed, secondary != null && secondary.isPressed,
                    stickClick != null && stickClick.isPressed, gripButton != null && gripButton.isPressed));
        return new TrackedHand(grip, aim, aim.IsValid && trigger != null ? Mathf.Clamp01(trigger.ReadValue()) : 0f,
            new ControllerInputs(stick != null, stick != null ? stick.ReadValue() : Vector2.zero,
                primary != null && primary.isPressed, secondary != null && secondary.isPressed,
                stickClick != null && stickClick.isPressed, gripButton != null && gripButton.isPressed,
                fullGamepad, Button("gamepadX"), Button("gamepadY"), Button("gamepadBumper"), Button("gamepadMenu"), Button("gamepadView"),
                new Vector2((Button("dpadRight") ? 1 : 0) - (Button("dpadLeft") ? 1 : 0), (Button("dpadUp") ? 1 : 0) - (Button("dpadDown") ? 1 : 0))));
    }

    private static TrackedPose ReadPose(PoseControl control)
    {
        if (control == null) return default;
        var pose = control.ReadValue();
        // Aim validity is independent of grip validity. Never substitute grip orientation for aim.
        return TrackedPose.FromSample(pose.isTracked, pose.trackingState, pose.position, pose.rotation);
    }
}
