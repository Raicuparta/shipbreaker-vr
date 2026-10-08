using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShipbreakerVr.Tracking;

public static class PresentationGeometry
{
    // Radius and stroke are proportional to eye distance, preserving apparent
    // size at a surface or the live grapple range. No gameplay range is changed.
    public static Vector2 CouchMarkerSize(float eyeDistance) =>
        new Vector2(Math.Max(.006f, eyeDistance * .004f), Math.Max(.0015f, eyeDistance * .0009f));

    // Keep the aim bearing when an endpoint is outside the headset camera's clip
    // range. This moves only the marker, never the raycast or tool reach.
    public static Vector3 CouchMarkerPosition(Vector3 eye, Vector3 target, float nearClip, float farClip)
    {
        var offset = target - eye;
        var distance = offset.magnitude;
        if (distance < .00001f) return target;
        var minimum = Math.Max(.3f, nearClip * 2f);
        var maximum = Math.Max(minimum, farClip * .8f);
        return eye + offset * (Math.Max(minimum, Math.Min(maximum, distance)) / distance);
    }

    public static bool IsCriticalHelmetEffect(string name) => name != null && name.StartsWith("FX_ShatteredGlass_Helmet_01", StringComparison.Ordinal);
    public static Vector2 CriticalHelmetFit(Vector2 nativeSize, float nativeDepth, Rect eyeBounds, float planeDistance)
    {
        var required = eyeBounds.size * (nativeDepth / planeDistance) * 2f;
        return new Vector2(Math.Max(1f, required.x / nativeSize.x), Math.Max(1f, required.y / nativeSize.y));
    }
    public static float DamageAxisScale(Vector3 axis, Vector2 fit) =>
        (float)Math.Sqrt(axis.x * axis.x * fit.x * fit.x + axis.y * axis.y * fit.y * fit.y + axis.z * axis.z);
    // Preserve the native crack's angular size/offset when moving it away from
    // the face. This deliberately has no headset-FOV or desktop-aspect input.
    public static bool DamageSpriteBounds(Vector3 nativeCenter, Vector2 nativeSize, float distance, out Rect rect)
    {
        rect = Rect.zero;
        if (nativeCenter.z <= .001f || distance <= 0) return false;
        var factor = distance / nativeCenter.z;
        var center = new Vector2(nativeCenter.x, nativeCenter.y) * factor;
        var size = new Vector2(Math.Abs(nativeSize.x), Math.Abs(nativeSize.y)) * factor;
        if (float.IsNaN(center.x + center.y + size.x + size.y) || float.IsInfinity(center.x + center.y + size.x + size.y)) return false;
        rect = new Rect(center - size * .5f, size);
        return true;
    }
    // Use the projection/view pair from XRDisplaySubsystem, as HDRP does. Legacy
    // Camera stereo APIs need not describe an SRP XR pass. Solve a corner ray in
    // right-handed eye space (-Z forward), then intersect the common head plane.
    public static bool DamageProjectionPoint(Matrix4x4 projection, Matrix4x4 eyeToHead, Vector2 corner, float distance, out Vector2 point)
    {
        point = Vector2.zero;
        var determinant = projection.m00 * projection.m11 - projection.m01 * projection.m10;
        if (Math.Abs(determinant) < .000001f || Math.Abs(projection.m32 + 1f) > .0001f || Math.Abs(projection.m33) > .0001f) return false;
        var x = corner.x + projection.m02;
        var y = corner.y + projection.m12;
        var origin = eyeToHead.MultiplyPoint3x4(Vector3.zero);
        var ray = eyeToHead.MultiplyVector(new Vector3(
            (x * projection.m11 - y * projection.m01) / determinant,
            (y * projection.m00 - x * projection.m10) / determinant, -1f));
        if (ray.z <= .0001f || distance <= origin.z) return false;
        var position = origin + ray * ((distance - origin.z) / ray.z);
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y)) return false;
        point = new Vector2(position.x, position.y);
        return true;
    }
    public static Rect IntersectClip(Rect a, Rect b)
    {
        var left = Math.Max(a.xMin, b.xMin); var right = Math.Min(a.xMax, b.xMax);
        var bottom = Math.Max(a.yMin, b.yMin); var top = Math.Min(a.yMax, b.yMax);
        return right > left && top > bottom ? new Rect(left, bottom, right - left, top - bottom) : Rect.zero;
    }
    public const int LoadingBackdropOrder = 30000;
    public static int UiSortOrder(bool loading, int rank) =>
        (loading ? LoadingBackdropOrder + 1 : 20000) + Math.Min(999, Math.Max(0, rank));
    public static Vector3 CentredPropOrigin(Vector3 target, Quaternion rotation, Vector3 nativeCenter, float scale) =>
        target - rotation * (nativeCenter * scale);

    public static int CompareUiOrder(int layerA, int orderA, int layerB, int orderB) =>
        layerA != layerB ? layerA.CompareTo(layerB) : orderA.CompareTo(orderB);

    public static Vector3 ToolPivot(Vector3 grip, Quaternion aim, Vector3 offset, float handleForward) =>
        grip + aim * (offset - Vector3.forward * handleForward);

    // Retain the calibrated forward reach, but put the actual muzzle on the aim line.
    public static Vector3 AlignedToolOrigin(Vector3 root, Vector3 rotatedMuzzle, Vector3 aimOrigin, Vector3 forward) =>
        aimOrigin + forward * Vector3.Dot(root + rotatedMuzzle - aimOrigin, forward) - rotatedMuzzle;

    public static Vector3 TopAlignedPropOrigin(Vector3 top, Quaternion rotation, Vector3 center, Vector3 extents, float scale, Vector3 up)
    {
        var height = scale * (Math.Abs(Vector3.Dot(rotation * Vector3.right, up)) * extents.x +
            Math.Abs(Vector3.Dot(rotation * Vector3.up, up)) * extents.y +
            Math.Abs(Vector3.Dot(rotation * Vector3.forward, up)) * extents.z);
        return CentredPropOrigin(top - up * height, rotation, center, scale);
    }

    public static Vector3 VisorPoint(float x, float y, float radius)
    {
        if (radius <= 0 || float.IsNaN(radius) || float.IsInfinity(radius)) return new Vector3(x, y, 0);
        radius = Math.Max(1.8f, radius);
        var horizontal = x / radius; var vertical = y / radius;
        return new Vector3(radius * (float)(Math.Sin(horizontal) * Math.Cos(vertical)),
            radius * (float)Math.Sin(vertical), radius * (float)(Math.Cos(horizontal) * Math.Cos(vertical) - 1));
    }

    public static float HudDepth(float xMetres, float radiusMetres) =>
        radiusMetres > 0 && !float.IsInfinity(radiusMetres) ? -xMetres * xMetres / (2 * radiusMetres) : 0;
    public static float VisorDepth(float xMetres, float yMetres, float radiusMetres) =>
        radiusMetres <= 0 ? 0 : HudDepth(xMetres, Math.Max(1.8f, radiusMetres)) + HudDepth(yMetres, Math.Max(1.8f, radiusMetres));

    public static void AppendTriangleGrid(List<int> indices, int steps, int start)
    {
        if (steps < 1 || steps > 16 || start < 0) throw new ArgumentOutOfRangeException();
        int Row(int row) => start + row * (steps + 1) - row * (row - 1) / 2;
        for (var row = 0; row < steps; row++) for (var col = 0; col < steps - row; col++)
        {
            var i = Row(row) + col; var j = Row(row + 1) + col; var k = i + 1;
            indices.Add(i); indices.Add(j); indices.Add(k);
            if (col < steps - row - 1) { indices.Add(j); indices.Add(j + 1); indices.Add(k); }
        }
    }

    // Construct both hands in one convention: fingers +Z, back of hand +Y.
    public static bool HandAxes(Vector3 wrist, Vector3 middle, Vector3 index, Vector3 pinky,
        bool left, out Vector3 forward, out Vector3 up)
    {
        forward = (middle - wrist).normalized;
        var across = (pinky - index) * (left ? -1 : 1);
        up = Vector3.Cross(forward, across).normalized;
        return forward.sqrMagnitude > .9f && up.sqrMagnitude > .9f;
    }

    public static bool ValidRange(float range) => range > .01f && !float.IsNaN(range) && !float.IsInfinity(range);
    public static float Endpoint(float range, float hitDistance) =>
        !ValidRange(range) ? 0 : hitDistance >= 0 && hitDistance < range ? hitDistance : range;
}
