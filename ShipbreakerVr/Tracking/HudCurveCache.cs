using System;
using UnityEngine;

namespace ShipbreakerVr.Tracking;

// Geometry is expressed relative to the HUD root, so moving the entire HUD with
// the head does not invalidate it. Never advance the saved key on a cache hit.
internal sealed class HudCurveCache
{
    private bool valid;
    private Matrix4x4 relative;
    private Vector3 scale;
    private Vector2 center;
    private float radius;

    internal void Invalidate() => valid = false;

    internal bool Matches(Matrix4x4 candidate, Vector3 candidateScale, Vector2 candidateCenter, float candidateRadius)
    {
        if (!valid || !center.Equals(candidateCenter) || radius != candidateRadius) return false;
        // Exact comparison of child layout, including tiny animation changes.
        for (var i = 0; i < 16; i++) if (relative[i] != candidate[i]) return false;
        // Lossy scale has floating-point noise when the rigid root rotates.
        // One part per million is <2 micrometres over this HUD, and cannot drift
        // cumulatively because comparisons always use the last rebuilt scale.
        return SameScale(scale.x, candidateScale.x) && SameScale(scale.y, candidateScale.y) && SameScale(scale.z, candidateScale.z);
    }

    private static bool SameScale(float a, float b) =>
        !float.IsNaN(a) && !float.IsNaN(b) && !float.IsInfinity(a) && !float.IsInfinity(b) &&
        Math.Abs(a) > 1e-8f && Math.Abs(b - a) <= Math.Abs(a) * 1e-6f;

    internal void Store(Matrix4x4 candidate, Vector3 candidateScale, Vector2 candidateCenter, float candidateRadius)
    {
        relative = candidate; scale = candidateScale; center = candidateCenter; radius = candidateRadius; valid = true;
    }
}
