using System;
using UnityEngine;
namespace ShipbreakerVr.Tracking;

internal static class RoomMarkerGeometry
{
    internal static Vector3 LocalScale(Vector3 nativeScale, Vector3 parentScale, float distance, float metresPerPixel)
    {
        var factor = metresPerPixel * Math.Max(2f, Math.Min(20f, distance));
        return new Vector3(nativeScale.x * factor / Math.Max(.000001f, Math.Abs(parentScale.x)),
            nativeScale.y * factor / Math.Max(.000001f, Math.Abs(parentScale.y)),
            nativeScale.z * factor / Math.Max(.000001f, Math.Abs(parentScale.z)));
    }
}
