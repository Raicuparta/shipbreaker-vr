using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnityEngine.UI;
using ShipbreakerVr.Tracking;
using UnityEngine;

namespace ShipbreakerVr;

// Operate on final UI geometry without adding components to ECS-owned game objects.
// Original meshes are restored before the next UI rebuild; curvature never accumulates.
internal sealed class VrHudCurve : IDisposable
{
    internal static bool CaptureAvailable;
    private static ConfigEntry<float> radius;
    private static ConfigEntry<float> panelWidth;
    private static ConfigEntry<float> verticalRoom;
    private static ConfigEntry<bool> followHead;
    private static readonly Dictionary<int, VrHudCurve> owners = new Dictionary<int, VrHudCurve>();
    private int canvasId;
    private Canvas ownedCanvas;
    private bool reportedCurve;
    internal void Register(Canvas canvas)
    {
        if (!CaptureAvailable || !IsHud(canvas)) return;
        ownedCanvas = canvas;
        canvasId = canvas.GetInstanceID(); owners[canvasId] = this;
        foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) graphic.SetVerticesDirty();
    }
    internal static void SubmitMesh(CanvasRenderer renderer, Mesh source)
    {
        using var measurement = new VrUiPerformance.Scope(VrUiPerformance.Work.MeshSubmission);
        // Unity 2020 has no CanvasRenderer.GetMesh. Capture only mesh submissions from
        // the UI/TMP managed pipeline, before its reusable worker mesh is overwritten.
        if (VrRoomMarkers.IsRoomGraphic(renderer)) { renderer.SetMesh(source); return; }
        var canvas = renderer.GetComponentInParent<Canvas>();
        if (canvas && canvas.rootCanvas) canvas = canvas.rootCanvas;
        if (canvas && owners.TryGetValue(canvas.GetInstanceID(), out var owner) && !owner.failed)
        {
            if (!owner.meshes.TryGetValue(renderer, out var item))
            { item = new SavedMesh(); owner.meshes.Add(renderer, item); }
            try
            {
                CopyMesh(source, item.Original); VrUiQuadSubdivision.Refine(item.Original, renderer.transform.lossyScale);
                CopyMesh(item.Original, item.Curved); item.Applied = false;
                item.Cache.Invalidate(); // Native geometry/colors/UVs may have changed.
                // TMP and other rebuilds can submit AFTER our willRenderCanvases callback.
                // Warp at the submission too, so a late rebuild cannot flatten the HUD.
                if (owner.Deform(renderer, item)) return;
            }
            catch (Exception error)
            {
                owner.failed = true; owner.Restore();
                Debug.LogWarning("[ShipbreakerVr] HUD mesh capture stopped; flat HUD retained. " + error);
            }
        }
        renderer.SetMesh(source);
    }
    private static readonly List<Vector3> copyVectors = new List<Vector3>();
    private static readonly List<Vector4> copyVectors4 = new List<Vector4>();
    private static readonly List<Color32> copyColors = new List<Color32>();
    private static readonly List<int> copyIndices = new List<int>();
    private static void CopyMesh(Mesh source, Mesh target)
    {
        target.Clear();
        if (!source || source.vertexCount == 0) return;
        target.indexFormat = source.indexFormat;
        source.GetVertices(copyVectors); target.SetVertices(copyVectors);
        source.GetNormals(copyVectors); if (copyVectors.Count > 0) target.SetNormals(copyVectors);
        source.GetTangents(copyVectors4); if (copyVectors4.Count > 0) target.SetTangents(copyVectors4);
        source.GetColors(copyColors); if (copyColors.Count > 0) target.SetColors(copyColors);
        for (var channel = 0; channel < 8; channel++)
        { source.GetUVs(channel, copyVectors4); if (copyVectors4.Count > 0) target.SetUVs(channel, copyVectors4); }
        target.subMeshCount = source.subMeshCount;
        for (var sub = 0; sub < source.subMeshCount; sub++)
        { source.GetIndices(copyIndices, sub); target.SetIndices(copyIndices, source.GetTopology(sub), sub); }
        target.bounds = source.bounds;
    }
    private readonly Dictionary<CanvasRenderer, SavedMesh> meshes = new Dictionary<CanvasRenderer, SavedMesh>();
    private readonly List<CanvasRenderer> renderers = new List<CanvasRenderer>();
    private readonly List<Vector3> vertices = new List<Vector3>();
    private bool failed;
    private sealed class SavedMesh
    {
        internal readonly Mesh Original = new Mesh(), Curved = new Mesh();
        internal readonly HudCurveCache Cache = new HudCurveCache();
        internal bool Applied;
    }
    internal static void Configure(ConfigFile config)
    {
        followHead = config.Bind("HUD", "FollowHead", true, "Move helmet and scanner HUD together with head rotation. F8 toggles this preference in game; false anchors both to body orientation.");
        panelWidth = config.Bind("HUD", "PanelWidthMetres", 1.6728f, new ConfigDescription("Helmet/scanner HUD width. Text and icons keep uniform scale; VerticalLayoutScale independently adds vertical room. Menus/loading retain their original size.", new AcceptableValueRange<float>(1.3f, 2.2f)));
        verticalRoom = config.Bind("HUD", "VerticalLayoutScale", 1.16f, new ConfigDescription("Additional vertical layout room for helmet/scanner HUD, without stretching text/icons. Menus/loading are unchanged.", new AcceptableValueRange<float>(1f, 1.5f)));
        radius = config.Bind("HUD", "VisorCurveRadiusMetres", 1.8f, new ConfigDescription("Helmet HUD curve in both directions. Larger is flatter; 0 disables curvature, positive values below 1.8 are clamped to 1.8 metres. Replaces CurveRadiusMetres.", new AcceptableValueRange<float>(0f, 30f)));
    }
    internal static bool IsHelmet(Canvas canvas) => canvas && canvas.name == "HUD Canvas - Helmet";
    private static bool IsHud(Canvas canvas) => canvas &&
        (IsHelmet(canvas) || canvas.name == "HUD Canvas - Other" || canvas.name == "OverlayHUDElements");
    internal static bool FollowsHead(Canvas canvas) => followHead.Value && IsHud(canvas);
    internal static float PanelWidth(Canvas canvas) => IsHud(canvas) ? panelWidth.Value : 2f;
    internal static float VerticalLayoutScale(Canvas canvas) => IsHud(canvas) ? verticalRoom.Value : 1f;
    internal static void ToggleFollowHead()
    {
        followHead.Value = !followHead.Value;
        Debug.Log($"[ShipbreakerVr] Helmet and scanner HUD follow {(followHead.Value ? "head" : "body")} together.");
    }
    internal void Apply(Canvas canvas)
    {
        using var measurement = new VrUiPerformance.Scope(VrUiPerformance.Work.Curvature);
        if (failed || !IsHud(canvas) || !(canvas.transform is RectTransform)) return;
        try
        {
            if (radius.Value <= 0) return;
            renderers.Clear(); canvas.GetComponentsInChildren(false, renderers);
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer.cull) continue;
                if (!meshes.TryGetValue(renderer, out var item)) continue;
                if (item.Applied) continue;
                if (item.Original.vertexCount == 0) continue;
                Deform(renderer, item);
            }
        }
        catch (Exception error)
        {
            failed = true; Restore();
            Debug.LogWarning("[ShipbreakerVr] HUD curvature stopped; flat HUD retained. " + error);
        }
    }
    private bool Deform(CanvasRenderer renderer, SavedMesh item)
    {
        if (radius.Value <= 0 || !ownedCanvas || !(ownedCanvas.transform is RectTransform root) || item.Original.vertexCount == 0) return false;
        var scale = root.lossyScale;
        // Compose local hierarchy transforms directly. Unlike cancelling two
        // world matrices, this key is exactly stable when the head/root moves.
        var toRoot = Matrix4x4.identity;
        var child = renderer.transform;
        while (child && child != root)
        {
            toRoot = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * toRoot;
            child = child.parent;
        }
        if (child != root) { item.Cache.Invalidate(); return false; }
        var center = root.rect.center;
        var curveRadius = radius.Value;
        if (item.Cache.Matches(toRoot, scale, center, curveRadius))
        {
            renderer.SetMesh(item.Curved); item.Applied = true;
            VrUiPerformance.CurveCacheHit();
            return true;
        }
        vertices.Clear(); item.Original.GetVertices(vertices);
        var fromRoot = toRoot.inverse;
        var divisorX = Mathf.Max(.00001f, Mathf.Abs(scale.x));
        var divisorY = Mathf.Max(.00001f, Mathf.Abs(scale.y));
        var divisorZ = Mathf.Max(.00001f, Mathf.Abs(scale.z));
        for (var i = 0; i < vertices.Count; i++)
        {
            var local = toRoot.MultiplyPoint3x4(vertices[i]);
            var x = (local.x - center.x) * scale.x;
            var y = (local.y - center.y) * scale.y;
            var curved = PresentationGeometry.VisorPoint(x, y, curveRadius);
            local.x = center.x + curved.x / divisorX;
            local.y = center.y + curved.y / divisorY;
            local.z += curved.z / divisorZ;
            vertices[i] = fromRoot.MultiplyPoint3x4(local);
        }
        item.Curved.SetVertices(vertices); item.Curved.RecalculateBounds();
        renderer.SetMesh(item.Curved); item.Applied = true;
        item.Cache.Store(toRoot, scale, center, curveRadius);
        VrUiPerformance.CurveRebuilt();
        if (!reportedCurve)
        {
            reportedCurve = true;
            Debug.Log($"[ShipbreakerVr] HUD spherical mesh applied: {ownedCanvas.name}; vertices={vertices.Count}; radius={radius.Value}; outline=removed");
        }
        return true;
    }
    internal void Restore()
    {
        using var measurement = new VrUiPerformance.Scope(VrUiPerformance.Work.MeshRestore);
        foreach (var pair in meshes)
        {
            if (pair.Key && pair.Value.Applied) pair.Key.SetMesh(pair.Value.Original);
            pair.Value.Applied = false;
        }
    }
    public void Dispose()
    {
        Restore();
        foreach (var pair in meshes)
        { UnityEngine.Object.Destroy(pair.Value.Original); UnityEngine.Object.Destroy(pair.Value.Curved); }
        meshes.Clear();
        if (owners.TryGetValue(canvasId, out var owner) && owner == this) owners.Remove(canvasId);
    }
}


[HarmonyPatch]
internal static class HudMeshCapturePatches
{
    private static Dictionary<MethodBase, int> targets;
    internal static void InstallOptional()
    {
        var harmony = new Harmony("ShipbreakerVr.HudCurve");
        VrHudCurve.CaptureAvailable = false;
        try
        {
            harmony.CreateClassProcessor(typeof(HudMeshCapturePatches)).Patch();
            VrHudCurve.CaptureAvailable = true;
            Debug.Log("[ShipbreakerVr] Optional HUD curvature initialized.");
        }
        catch (Exception error)
        {
            // Core XR/controller components already exist. Any surviving partial hooks
            // are inert because no curve owner can register while capture is unavailable.
            try { harmony.UnpatchSelf(); }
            catch (Exception cleanup) { Debug.LogWarning("[ShipbreakerVr] Optional HUD hook cleanup: " + cleanup); }
            Debug.LogWarning("[ShipbreakerVr] HUD curvature unavailable; flat HUD retained and core VR/controller startup continues. " + error);
        }
    }
    private static IEnumerable<MethodBase> TargetMethods()
    {
        targets = HudMeshPatchTargets.Resolve();
        return targets.Keys;
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = new List<CodeInstruction>(instructions); var count = 0;
        var submit = AccessTools.Method(typeof(CanvasRenderer), "SetMesh", new[] { typeof(Mesh) });
        foreach (var item in code)
            if (Equals(item.operand, submit))
            { item.opcode = OpCodes.Call; item.operand = AccessTools.Method(typeof(VrHudCurve), "SubmitMesh"); count++; }
        if (count != targets[original]) throw new InvalidOperationException("HUD mesh pipeline changed: " + original);
        Debug.Log($"[ShipbreakerVr] HUD mesh capture verified: {original.Name}, calls={count}");
        return code;
    }
}
