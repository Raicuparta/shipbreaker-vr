using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// Render the game's prop without re-enabling the hidden animated body hierarchy.
// No scripts, colliders, skeleton bones or native mesh/material assets are changed.
internal sealed class VrHandPropVisuals : IDisposable
{
    private readonly List<Part> parts = new List<Part>();
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private bool failed, logged;
    private readonly string label;
    internal VrHandPropVisuals(string label = "Detonator") { this.label = label; }
    private sealed class Part
    {
        internal Renderer Source;
        internal Mesh Mesh;
        internal SkinnedMeshRenderer Skin;
        internal bool OwnsMesh;
        internal Matrix4x4 Relative;
    }
    internal void Register(Renderer[] sources)
    {
        Dispose(); failed = false; logged = false;
        foreach (var source in sources)
        {
            if (!source) continue;
            if (source is SkinnedMeshRenderer skin && skin.sharedMesh)
                parts.Add(new Part { Source = source, Skin = skin, Mesh = new Mesh(), OwnsMesh = true });
            else if (source is MeshRenderer && source.GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh)
                parts.Add(new Part { Source = source, Mesh = filter.sharedMesh });
        }
        Debug.Log($"[ShipbreakerVr] {label} presentation registered: {parts.Count} mesh parts.");
    }
    internal void Draw(Transform attachment, Vector3 position, Quaternion rotation, float scale, Camera camera, Vector3? topUp = null)
        => DrawCore(attachment, position, rotation, scale, camera, topUp, false);
    internal bool DrawAtNativePose(float scale, Camera camera)
        => DrawCore(null, Vector3.zero, Quaternion.identity, scale, camera, null, true);
    private static bool NativeVisible(Part part) => part.Source && part.Source.enabled &&
        part.Source.gameObject.activeInHierarchy && !part.Source.forceRenderingOff;
    private bool DrawCore(Transform attachment, Vector3 position, Quaternion rotation, float scale, Camera camera, Vector3? topUp, bool nativePose)
    {
        if (failed) return false;
        try
        {
            var layer = 0; while (layer < 31 && (camera.cullingMask & (1 << layer)) == 0) layer++;
            var bounds = new Bounds(); var hasBounds = false;
            foreach (var part in parts)
            {
                if (!part.Source || !part.Mesh) continue;
                if (nativePose && !NativeVisible(part)) continue;
                if (part.Skin) part.Skin.BakeMesh(part.Mesh);
                part.Relative = nativePose ? part.Source.localToWorldMatrix : attachment.worldToLocalMatrix * part.Source.localToWorldMatrix;
                var box = part.Mesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = part.Relative.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!hasBounds) return false;
            // Couch geometry is in world space: scaling around its world centre
            // keeps native placement/orientation and avoids touching animated bones.
            if (nativePose) position = bounds.center;
            // Remove the native arm/animation translation, anchoring visible geometry.
            var origin = topUp.HasValue ? PresentationGeometry.TopAlignedPropOrigin(position, rotation, bounds.center, bounds.extents, scale, topUp.Value)
                : PresentationGeometry.CentredPropOrigin(position, rotation, bounds.center, scale);
            var centredToWorld = Matrix4x4.TRS(origin, rotation, Vector3.one * scale);
            foreach (var part in parts)
            {
                if (!part.Source || !part.Mesh) continue;
                if (nativePose && !NativeVisible(part)) continue;
                var matrix = centredToWorld * part.Relative;
                var materials = part.Source.sharedMaterials;
                properties.Clear(); part.Source.GetPropertyBlock(properties);
                for (var sub = 0; sub < part.Mesh.subMeshCount && sub < materials.Length; sub++)
                    if (materials[sub]) Graphics.DrawMesh(part.Mesh, matrix, materials[sub], layer, camera, sub, properties, ShadowCastingMode.Off, false);
            }
            if (!logged && parts.Count > 0)
            { logged = true; Debug.Log($"[ShipbreakerVr] {label} presentation: nativePose={nativePose}; centre={bounds.center}; native size={bounds.size}; scale={scale}"); }
            return true;
        }
        catch (Exception error)
        { failed = true; Debug.LogWarning($"[ShipbreakerVr] {label} visual unavailable; other tools remain active. " + error); return false; }
    }
    public void Dispose()
    {
        foreach (var part in parts) if (part.OwnsMesh && part.Mesh) UnityEngine.Object.Destroy(part.Mesh);
        parts.Clear();
    }
}
