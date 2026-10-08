using UnityEngine;
using UnityEngine.XR;

namespace ShipbreakerVr.Tracking;

/// <summary>Metres, Unity coordinates, in the runtime's tracking space.</summary>
public readonly struct TrackedPose
{
    public readonly bool IsValid;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;

    public TrackedPose(Vector3 position, Quaternion rotation)
    {
        IsValid = true;
        Position = position;
        Rotation = rotation;
    }

    public static TrackedPose FromSample(bool tracked, InputTrackingState state, Vector3 position, Quaternion rotation)
    {
        const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
        if (!tracked || (state & required) != required ||
            !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
            !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w) ||
            rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w < 0.0001f)
            return default;
        return new TrackedPose(position, rotation);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public readonly struct TrackedHand
{
    public readonly TrackedPose Grip;
    public readonly TrackedPose Aim;
    public readonly float Trigger;
    public readonly ControllerInputs Inputs;

    public TrackedHand(TrackedPose grip, TrackedPose aim, float trigger, ControllerInputs inputs = default)
    {
        Grip = grip;
        Aim = aim;
        Trigger = trigger;
        Inputs = inputs;
    }
}

/// <summary>No game types or gameplay actions cross this boundary.</summary>
public interface IVrTrackingProvider
{
    TrackedPose Head { get; }
    TrackedHand LeftHand { get; }
    TrackedHand RightHand { get; }
    void Sample();
    void Clear();
}
