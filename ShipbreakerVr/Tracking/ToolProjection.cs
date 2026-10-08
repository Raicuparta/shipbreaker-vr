using System;
using UnityEngine;

namespace ShipbreakerVr.Tracking;

// Game screen coordinates may use eye-texture dimensions, not the desktop camera rect.
public static class ToolProjection
{
    public static Vector3 Direction(Vector2 pixel, Vector2 size, float verticalFov, float aspect)
    {
        if (!MovementInput.Finite(pixel) || !MovementInput.Finite(size) || size.x <= 0 || size.y <= 0 ||
            float.IsNaN(verticalFov) || float.IsInfinity(verticalFov) || float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0)
            return Vector3.forward;
        var halfHeight = (float)Math.Tan(Math.Max(1, Math.Min(179, verticalFov)) * Math.PI / 360);
        return new Vector3((pixel.x / size.x * 2 - 1) * halfHeight * aspect,
            (pixel.y / size.y * 2 - 1) * halfHeight, 1).normalized;
    }
}
