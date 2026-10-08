using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ShipbreakerVr.Tracking;

/// <summary>Reads the OpenXR provider's XR SDK features in legacy-input Unity players.</summary>
public sealed class NativeXrHandReader
{
    private readonly XRNode node;
    private readonly List<InputFeatureUsage> features = new List<InputFeatureUsage>();
    private InputDevice? cachedDevice;
    private string aimPosition;
    private string aimRotation;
    private string aimTracked;
    private string aimState;
    private bool useSharedValidity;
    private string lastDiscovery;
    private string description = "not sampled";
    public string Description => description;

    public NativeXrHandReader(XRNode node) => this.node = node;

    public void Clear() { cachedDevice = null; description = "not sampled"; }

    public TrackedHand Read()
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) { Clear(); return default; }
        if (!cachedDevice.HasValue || !cachedDevice.Value.Equals(device)) Discover(device);

        var grip = default(TrackedPose);
        if (device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) &&
            device.TryGetFeatureValue(CommonUsages.trackingState, out var state) &&
            device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) &&
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
            grip = TrackedPose.FromSample(tracked, state, position, rotation);

        // Some old OpenXR XR SDK providers publish PointerPosition/Rotation but expose
        // both poses' tracking metadata under shared IsTracked/TrackingState names.
        // Prefer independent flags; in that compatibility case use device-level flags
        // while still requiring actual pointer data, never substituting a grip pose.
        var aim = default(TrackedPose);
        var validityTracked = useSharedValidity ? CommonUsages.isTracked.name : aimTracked;
        var validityState = useSharedValidity ? CommonUsages.trackingState.name : aimState;
        if (aimPosition != null && aimRotation != null && validityTracked != null && validityState != null &&
            device.TryGetFeatureValue(new InputFeatureUsage<bool>(validityTracked), out var pointerTracked) &&
            device.TryGetFeatureValue(new InputFeatureUsage<uint>(validityState), out var pointerState) &&
            device.TryGetFeatureValue(new InputFeatureUsage<Vector3>(aimPosition), out var pointerPosition) &&
            device.TryGetFeatureValue(new InputFeatureUsage<Quaternion>(aimRotation), out var pointerRotation))
            aim = TrackedPose.FromSample(pointerTracked, (InputTrackingState)pointerState, pointerPosition, pointerRotation);
        device.TryGetFeatureValue(CommonUsages.trigger, out var trigger);
        var stickAvailable = device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var stick);
        device.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);
        device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary);
        device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var stickClick);
        device.TryGetFeatureValue(CommonUsages.gripButton, out var gripButton);
        bool Button(string usage) => device.TryGetFeatureValue(new InputFeatureUsage<bool>(usage), out var value) && value;
        // The custom bumper usage is available on both hands, even when released.
        var fullGamepad = device.TryGetFeatureValue(new InputFeatureUsage<bool>("GamepadBumper"), out var bumper);
        if (!fullGamepad) return new TrackedHand(grip, aim, aim.IsValid ? Mathf.Clamp01(trigger) : 0f,
            new ControllerInputs(stickAvailable, stick, primary, secondary, stickClick, gripButton));
        return new TrackedHand(grip, aim, aim.IsValid ? Mathf.Clamp01(trigger) : 0f,
            new ControllerInputs(stickAvailable, stick, primary, secondary, stickClick, gripButton,
                fullGamepad, Button("GamepadX"), Button("GamepadY"), bumper, Button("GamepadMenu"), Button("GamepadView"),
                new Vector2((Button("GamepadDpadRight") ? 1 : 0) - (Button("GamepadDpadLeft") ? 1 : 0),
                    (Button("GamepadDpadUp") ? 1 : 0) - (Button("GamepadDpadDown") ? 1 : 0))));
    }

    private void Discover(InputDevice device)
    {
        cachedDevice = device;
        aimPosition = aimRotation = aimTracked = aimState = null;
        useSharedValidity = false;
        var sharedTracked = false;
        var sharedState = false;
        features.Clear();
        device.TryGetFeatureUsages(features);
        var names = new List<string>();
        foreach (var feature in features)
        {
            names.Add(feature.name + ":" + feature.type.Name);
            if (feature.name == CommonUsages.isTracked.name && feature.type == typeof(bool)) sharedTracked = true;
            if (feature.name == CommonUsages.trackingState.name && feature.type == typeof(uint)) sharedState = true;
            var semantic = NativeAimFeature.Classify(feature.name);
            if (semantic == NativeAimFeatureKind.Position && feature.type == typeof(Vector3)) aimPosition = feature.name;
            if (semantic == NativeAimFeatureKind.Rotation && feature.type == typeof(Quaternion)) aimRotation = feature.name;
            if (semantic == NativeAimFeatureKind.IsTracked && feature.type == typeof(bool)) aimTracked = feature.name;
            if (semantic == NativeAimFeatureKind.TrackingState &&
                (feature.type == typeof(uint) || feature.type == typeof(InputTrackingState))) aimState = feature.name;
        }
        useSharedValidity = NativeAimFeature.CanUseSharedValidity(aimPosition != null, aimRotation != null,
            aimTracked != null, aimState != null, sharedTracked, sharedState);
        description = $"{device.name}; aimFields={aimPosition}/{aimRotation}; validity={(useSharedValidity ? "shared-device flags" : aimTracked + "/" + aimState)}";
        var discovery = description + "; features=" + string.Join(", ", names);
        if (discovery != lastDiscovery)
        {
            lastDiscovery = discovery;
            Debug.Log($"[ShipbreakerVr] {node} OpenXR XR SDK: {discovery}");
        }
    }
}
