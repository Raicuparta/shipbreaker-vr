using System;
using UnityEngine;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// The critical red effect is a lit, curved mesh, not FX_VisorCrack's white quad.
// Keep its material, lights and pooled lifetime. Only override its pose/scale for
// rendering, then restore before the game can sample the native helmet again.
internal sealed class CriticalHelmetVisual
{
    private readonly MeshRenderer renderer;
    private readonly MeshFilter filter;
    private readonly Light[] lights;
    private readonly float[] ranges, intensities;
    private Vector3 position, scale;
    private Quaternion rotation;
    private bool applied, reported;

    internal CriticalHelmetVisual(MeshRenderer renderer)
    {
        this.renderer = renderer;
        filter = renderer.GetComponent<MeshFilter>();
        lights = renderer.GetComponentsInChildren<Light>(true);
        ranges = new float[lights.Length]; intensities = new float[lights.Length];
    }
    internal bool Exists => renderer;
    internal bool Active => renderer && renderer.enabled && renderer.gameObject.activeInHierarchy && filter && filter.sharedMesh;

    internal void Apply(Camera native, Camera view, Rect eyeBounds, float planeDistance)
    {
        if (!Active || !native || !view) return;
        var target = renderer.transform;
        var bounds = filter.sharedMesh.bounds;
        var center = native.transform.InverseTransformPoint(target.TransformPoint(bounds.center));
        if (center.z <= .001f) return;
        var cameraRotation = Quaternion.Inverse(native.transform.rotation) * target.rotation;
        var nativeSize = Vector3.Scale(bounds.size, target.lossyScale);
        var right = cameraRotation * Vector3.right;
        var up = cameraRotation * Vector3.up;
        var forward = cameraRotation * Vector3.forward;
        var width = Mathf.Abs(right.x * nativeSize.x) + Mathf.Abs(up.x * nativeSize.y) + Mathf.Abs(forward.x * nativeSize.z);
        var height = Mathf.Abs(right.y * nativeSize.x) + Mathf.Abs(up.y * nativeSize.y) + Mathf.Abs(forward.y * nativeSize.z);
        if (width <= .0001f || height <= .0001f) return;
        // Enclose both eye rectangles inside the curved visor, including the
        // circular perimeter. Preserve physical depth and native surface shading.
        var fit = PresentationGeometry.CriticalHelmetFit(new Vector2(width, height), center.z, eyeBounds, planeDistance);
        var fitX = fit.x; var fitY = fit.y;
        position = target.position; rotation = target.rotation; scale = target.localScale;
        for (var i = 0; i < lights.Length; i++)
            if (lights[i]) { ranges[i] = lights[i].range; intensities[i] = lights[i].intensity; }
        applied = true;
        target.rotation = view.transform.rotation * cameraRotation;
        target.localScale = Vector3.Scale(scale, new Vector3(PresentationGeometry.DamageAxisScale(right, fit), PresentationGeometry.DamageAxisScale(up, fit), PresentationGeometry.DamageAxisScale(forward, fit)));
        var offset = eyeBounds.center * (center.z / planeDistance);
        var desiredCenter = view.transform.TransformPoint(new Vector3(offset.x, offset.y, center.z));
        target.position += desiredCenter - target.TransformPoint(bounds.center);
        // The effect's own lights are children of this mesh. Their influence must
        // grow with their separation; all values are restored after rendering.
        var lightScale = Mathf.Max(fitX, fitY);
        for (var i = 0; i < lights.Length; i++)
            if (lights[i]) { lights[i].range = ranges[i] * lightScale; lights[i].intensity = intensities[i] * lightScale * lightScale; }
        if (!reported)
        {
            reported = true;
            Debug.Log($"[ShipbreakerVr] Critical shattered helmet fitted to XR view: mesh={renderer.name}; fit={fitX:F2}/{fitY:F2}; nativeDepth={center.z:F3}; native material and {lights.Length} lights retained.");
        }
    }
    internal void Restore()
    {
        if (!applied) return;
        if (renderer)
        {
            renderer.transform.SetPositionAndRotation(position, rotation);
            renderer.transform.localScale = scale;
        }
        for (var i = 0; i < lights.Length; i++)
            if (lights[i]) { lights[i].range = ranges[i]; lights[i].intensity = intensities[i]; }
        applied = false;
    }
}
