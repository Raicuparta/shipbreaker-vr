using System;
using UnityEngine;

namespace ShipbreakerVr.Tracking;

public static class ArmGeometry
{
    // Preserve both bone lengths, including targets beyond reach or inside the shoulder.
    public static bool Solve(Vector3 shoulder, Vector3 target, Vector3 pole, float upper, float lower,
        out Vector3 elbow, out Vector3 wrist)
    {
        elbow = wrist = shoulder;
        if (upper < .001f || lower < .001f || float.IsNaN(upper) || float.IsNaN(lower) ||
            float.IsInfinity(upper) || float.IsInfinity(lower)) return false;
        var delta = target - shoulder;
        var distance = delta.magnitude;
        if (float.IsNaN(distance) || float.IsInfinity(distance)) return false;
        var direction = distance > .0001f ? delta / distance : Vector3.forward;
        distance = Math.Max(Math.Abs(upper - lower) + .0001f, Math.Min(upper + lower - .0001f, distance));
        var bend = pole - shoulder;
        bend -= direction * Vector3.Dot(bend, direction);
        if (bend.sqrMagnitude < .0001f)
        {
            bend = Vector3.Cross(direction, Vector3.up);
            if (bend.sqrMagnitude < .0001f) bend = Vector3.Cross(direction, Vector3.right);
        }
        var along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
        var away = (float)Math.Sqrt(Math.Max(0, upper * upper - along * along));
        wrist = shoulder + direction * distance;
        elbow = shoulder + direction * along + bend.normalized * away;
        return true;
    }
}
