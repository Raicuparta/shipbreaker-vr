using System;
using System.Collections.Generic;
using ShipbreakerVr.Tracking;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipbreakerVr;

// Bake only glove triangles from the local game's mesh. No game assets are distributed,
// no arm bones are moved, and the cached meshes survive habitat/yard scene transitions.
internal sealed class VrGloveVisuals : IDisposable
{
    private MeshRenderer left, right;
    private float nextAttempt;
    private bool reported;
    private bool drawFailed;
    private string lastDrawStatus;
    internal void TryCapture(Animator animator)
    {
        if (!animator || (left && right) || Time.unscaledTime < nextAttempt) return;
        nextAttempt = Time.unscaledTime + 3;
        try
        {
            // Capture gloves only from the live suited character. Menus use controllers.
            var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skin in skins)
            {
                if (skin.name != "geo_arms" || !skin.sharedMesh) continue;
                if (!left) left = Capture(skin, true);
                if (!right) right = Capture(skin, false);
                if (left && right) break;
            }

        }
        catch (Exception error)
        {
            if (!reported) Debug.LogWarning("[ShipbreakerVr] Glove extraction unavailable; native arms stay hidden. " + error);
            reported = true;
        }
    }

    private static MeshRenderer Capture(SkinnedMeshRenderer skin, bool isLeft)
    {
        var prefix = isLeft ? "rig_left_hand" : "rig_right_hand";
        var bones = skin.bones;
        Transform Find(string suffix) => Array.Find(bones, b => b && b.name == prefix + suffix);
        // Metacarpal base helpers can coincide with the wrist. Use knuckle joints.
        var wrist = Find(""); var middle = Find("_middle1"); var index = Find("_index1"); var pinky = Find("_pinky1");
        if (!wrist || !middle || !index || !pinky ||
            !PresentationGeometry.HandAxes(wrist.position, middle.position, index.position, pinky.position, isLeft, out var forward, out var up)) return null;
        var inverse = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
        var baked = new Mesh();
        Mesh mesh = null;
        GameObject host = null;
        try
        {
            skin.BakeMesh(baked);
            var sourceVertices = baked.vertices;
            var canonical = new Vector3[sourceVertices.Length];
            for (var i = 0; i < canonical.Length; i++)
                canonical[i] = inverse * (skin.transform.TransformPoint(sourceVertices[i]) - wrist.position);
            var readable = skin.sharedMesh.isReadable;
            var weights = readable ? skin.sharedMesh.boneWeights : null;
            var selectedBones = new bool[bones.Length];
            for (var i = 0; i < bones.Length; i++) selectedBones[i] = bones[i] && (bones[i] == wrist || bones[i].IsChildOf(wrist));
            float Weight(int b, float w) => b >= 0 && b < selectedBones.Length && selectedBones[b] ? w : 0;
            var palmWidth = Vector3.Distance(index.position, pinky.position);
            var palmLength = Vector3.Distance(wrist.position, middle.position);
            if (palmWidth < .015f || palmLength < .025f) return null;
            bool Select(int i)
            {
                var p = canonical[i];
                // Wrist plane prevents a sleeve even when forearm vertices share wrist weights.
                if (p.z < -.02f || p.z > palmLength * 3 || Math.Abs(p.x) > palmWidth * 1.8f || Math.Abs(p.y) > palmWidth * 1.8f) return false;
                if (weights == null || weights.Length != canonical.Length) return true;
                var w = weights[i];
                return Weight(w.boneIndex0, w.weight0) + Weight(w.boneIndex1, w.weight1) +
                    Weight(w.boneIndex2, w.weight2) + Weight(w.boneIndex3, w.weight3) >= .5f;
            }
            var remap = new Dictionary<int, int>();
            var originals = new List<int>();
            var positions = new List<Vector3>();
            int Add(int i)
            {
                if (remap.TryGetValue(i, out var mapped)) return mapped;
                mapped = positions.Count; remap.Add(i, mapped); positions.Add(canonical[i]); originals.Add(i); return mapped;
            }
            var triangles = new List<int>[baked.subMeshCount];
            for (var sub = 0; sub < triangles.Length; sub++)
            {
                triangles[sub] = new List<int>();
                var source = baked.GetTriangles(sub);
                for (var i = 0; i + 2 < source.Length; i += 3)
                    if (Select(source[i]) && Select(source[i + 1]) && Select(source[i + 2]))
                    { triangles[sub].Add(Add(source[i])); triangles[sub].Add(Add(source[i + 1])); triangles[sub].Add(Add(source[i + 2])); }
            }
            if (positions.Count < 60) return null;
            mesh = new Mesh { name = prefix + " VR glove", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            var normals = baked.normals;
            if (normals.Length == canonical.Length)
            {
                var values = new List<Vector3>();
                foreach (var i in originals) values.Add(inverse * skin.transform.TransformDirection(normals[i]));
                mesh.SetNormals(values);
            }
            var tangents = baked.tangents;
            if (tangents.Length == canonical.Length)
            {
                var values = new List<Vector4>();
                foreach (var i in originals)
                {
                    var t = tangents[i]; var xyz = inverse * skin.transform.TransformDirection(new Vector3(t.x, t.y, t.z));
                    values.Add(new Vector4(xyz.x, xyz.y, xyz.z, t.w));
                }
                mesh.SetTangents(values);
            }
            for (var channel = 0; channel < 8; channel++)
            {
                var uv = new List<Vector4>(); baked.GetUVs(channel, uv);
                if (uv.Count != canonical.Length) continue;
                var compact = new List<Vector4>(); foreach (var i in originals) compact.Add(uv[i]); mesh.SetUVs(channel, compact);
            }
            mesh.subMeshCount = triangles.Length;
            for (var sub = 0; sub < triangles.Length; sub++) mesh.SetTriangles(triangles[sub], sub);
            if (normals.Length != canonical.Length) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (mesh.bounds.size.z < .06f || mesh.bounds.size.z > .5f) { UnityEngine.Object.Destroy(mesh); return null; }
            // Normalize the captured glove length to 19 cm; keep both hands on a human scale.
            var size = .19f / mesh.bounds.size.z;
            host = new GameObject(prefix + " VR isolated glove");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.transform.localScale = Vector3.one * size;
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.AddComponent<MeshRenderer>();
            var materials = skin.sharedMaterials;
            for (var i = 0; i < materials.Length; i++) if (materials[i]) materials[i] = new Material(materials[i]) { renderQueue = 3100 };
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false;
            Debug.Log($"[ShipbreakerVr] Cached {prefix} glove: vertices={positions.Count}, bounds={mesh.bounds.size}, scale={size:F3}, selection={(readable ? "bone weights" : "wrist spatial clip")}");
            return renderer;
        }
        catch
        {
            if (host) UnityEngine.Object.Destroy(host);
            if (mesh) UnityEngine.Object.Destroy(mesh);
            throw;
        }
        finally { UnityEngine.Object.Destroy(baked); }
    }

    internal void Draw(OpenXrTrackingProvider tracking, Transform body, Camera camera, bool aboveUi)
    {
        if (drawFailed) return;
        try { DrawHands(tracking, body, camera, aboveUi); }
        catch (Exception error)
        { drawFailed = true; Hide(); Debug.LogWarning("[ShipbreakerVr] Glove display unavailable; tool presentation continues. " + error); }
    }
    private void DrawHands(OpenXrTrackingProvider tracking, Transform body, Camera camera, bool aboveUi)
    {
        // Menus use rays. Only display actual captured suit gloves in gameplay.
        var showLeft = left;
        var showRight = right;
        DrawHand(showLeft, tracking.LeftHand, showLeft == left);
        DrawHand(showRight, tracking.RightHand, showRight == right);
        var status = $"menu={aboveUi}; left={tracking.LeftHand.Grip.IsValid}/{(showLeft == left ? "native glove" : "controller")}; right={tracking.RightHand.Grip.IsValid}/{(showRight == right ? "native glove" : "controller")}; near={camera.nearClipPlane}";
        if (status != lastDrawStatus)
        {
            lastDrawStatus = status;
            Debug.Log("[ShipbreakerVr] Glove draw: " + status);
            foreach (var renderer in new[] { showLeft, showRight })
                if (renderer) Debug.Log($"[ShipbreakerVr] Glove view {renderer.name}: centre={camera.WorldToViewportPoint(renderer.bounds.center)}; size={renderer.bounds.size}; layer={renderer.gameObject.layer}; enabled={renderer.enabled}");
        }
        void DrawHand(MeshRenderer renderer, TrackedHand hand, bool isGlove)
        {
            if (!renderer || !hand.Grip.IsValid) return;
            var pose = ShipbreakerTrackingSpace.ToWorld(tracking.Head, hand.Grip, body.position, body.rotation, VrCamera.EyeOffset);
            var layer = 0; while (layer < 31 && (camera.cullingMask & (1 << layer)) == 0) layer++;
            renderer.gameObject.layer = layer;
            var topLayer = 0; var topValue = int.MinValue;
            if (aboveUi) foreach (var sortingLayer in SortingLayer.layers)
                if (sortingLayer.value > topValue) { topValue = sortingLayer.value; topLayer = sortingLayer.id; }
            renderer.sortingLayerID = topLayer;
            renderer.sortingOrder = aboveUi ? 32767 : 0;
            foreach (var material in renderer.sharedMaterials)
            {
                if (!material) continue;
                // Opaque HDRP materials can demand Equal to a depth prepass that
                // queue3100 never ran. Set the actual Forward pass depth uniform.
                var depth = aboveUi ? CompareFunction.Always : CompareFunction.LessEqual;
                foreach (var key in new[] { "_ZTestDepthEqualForOpaque", "_ZTestTransparent", "_ZTest", "_ZTestMode" })
                    if (material.HasProperty(key)) material.SetInt(key, (int)depth);
                if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            }
            // Grip origin lies in the palm; the extracted mesh origin is at the wrist.
            renderer.transform.SetPositionAndRotation(pose.position - pose.rotation * Vector3.forward * (isGlove ? .07f : 0), pose.rotation);
            renderer.enabled = true;
        }
    }
    internal void Hide() { foreach (var renderer in new[] { left, right }) if (renderer) renderer.enabled = false; }
    public void Dispose()
    {
        foreach (var renderer in new[] { left, right })
        {
            if (!renderer) continue;
            foreach (var mat in renderer.sharedMaterials) if (mat) UnityEngine.Object.Destroy(mat);
            UnityEngine.Object.Destroy(renderer.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.Destroy(renderer.gameObject);
        }
    }
}
