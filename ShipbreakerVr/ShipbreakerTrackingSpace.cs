using ShipbreakerVr.Tracking;
using UnityEngine;

namespace ShipbreakerVr;

/// <summary>Adapts tracking space to the original mod's rotation-only view.</summary>
public static class ShipbreakerTrackingSpace
{
    public static Pose ToWorld(TrackedPose head, TrackedPose controller, Vector3 bodyPosition, Quaternion bodyRotation, Vector3 eyeOffset)
    {
        // The game camera supplies body orientation. HMD rotation is applied by its child
        // TrackedPoseDriver, so applying the VR camera rotation here would rotate hands twice.
        var position = bodyPosition + bodyRotation * (eyeOffset + controller.Position - head.Position);
        var rotation = bodyRotation * controller.Rotation;
        return new Pose(position, rotation);
    }
}
