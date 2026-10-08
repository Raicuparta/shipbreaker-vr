using System;
using System.Collections.Generic;
using BBI.Unity.Game;
using Carbon.Core.Unity;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// Mirror game-owned damage graphics onto one head-relative stereo surface. The
// native FX pool, repair conditions, sprite selection and fade timers remain owners.
[DefaultExecutionOrder(11000)]
internal sealed class VrHelmetDamage : MonoBehaviour
{
    // Low disparity while the player looks at distant ships. Depth testing is
    // disabled for these graphics, so this is visual depth, not world occlusion.
    private const float Distance = 20f;
    private const float PixelsPerMetre = 1000f;
    private static VrHelmetDamage instance;
    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<MeshRenderer, CriticalHelmetVisual> critical = new Dictionary<MeshRenderer, CriticalHelmetVisual>();
    private Canvas canvas;
    private Material material;
    private bool failed;
    private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
    private static readonly Vector2[] corners = { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) };
    private Rect coverage;
    private bool hasCoverage;
    private string coverageStatus;
    internal static int VisibleLayerMask => instance && instance.canvas ? 1 << instance.canvas.gameObject.layer : 0;

    private sealed class Entry
    {
        internal Image SourceImage;
        internal Renderer SourceRenderer;
        internal Graphic Proxy;
        internal Color SavedColor;
        internal bool SavedHidden, Suppressed;
        internal Sprite ReportedSprite;
    }

    private void Awake() { instance = this; gameObject.AddComponent<VrHelmetDamageEarlyRestore>(); }
    private void OnEnable()
    {
        RenderPipelineManager.endFrameRendering += AfterFrame;
        RenderPipelineManager.beginFrameRendering += BeforeFrame;
        Canvas.willRenderCanvases += SafePlace;
    }
    internal static void RestoreEarly() { if (instance) instance.Restore(); }
    private void AfterFrame(ScriptableRenderContext context, Camera[] cameras) => Restore();
    private void BeforeFrame(ScriptableRenderContext context, Camera[] cameras) => SafePlace();

    internal static void RegisterCanvas(Canvas root)
    {
        if (!instance || !root) return;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
            if (image.name == "Damage Overlay") RegisterImage(image);
    }
    internal static void RegisterImage(Image image)
    {
        if (!instance || !image) return;
        foreach (var entry in instance.entries) if (entry.SourceImage == image) return;
        instance.entries.Add(new Entry { SourceImage = image });
        Debug.Log("[ShipbreakerVr] Full-view damage image registered: " + image.name);
    }
    internal static void RegisterCrack(FXElement fx)
    {
        if (!instance || !fx) return;
        if (PresentationGeometry.IsCriticalHelmetEffect(fx.name))
        {
            var stale = new List<MeshRenderer>();
            foreach (var pair in instance.critical) if (!pair.Value.Exists) stale.Add(pair.Key);
            foreach (var key in stale) instance.critical.Remove(key);
            foreach (var mesh in fx.GetComponentsInChildren<MeshRenderer>(true))
                if (!instance.critical.ContainsKey(mesh))
                {
                    instance.critical.Add(mesh, new CriticalHelmetVisual(mesh));
                    Debug.Log("[ShipbreakerVr] Critical shattered-helmet visual registered: " + fx.name + "/" + mesh.name);
                }
            return;
        }
        if (!fx.name.StartsWith("FX_VisorCrack", StringComparison.Ordinal)) return;
        foreach (var renderer in fx.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is SpriteRenderer) && !(renderer is MeshRenderer)) continue;
            if (instance.entries.Exists(entry => entry.SourceRenderer == renderer)) continue;
            instance.entries.Add(new Entry { SourceRenderer = renderer });
            Debug.Log("[ShipbreakerVr] Stereo visor visual registered: " + fx.name + "/" + renderer.name);
        }
    }

    private void EnsureCanvas()
    {
        if (canvas) return;
        var host = new GameObject("ShipbreakerVr damage surface", typeof(RectTransform), typeof(Canvas));
        // Parenting also follows the late tracked-pose update before rendering.
        host.transform.SetParent(VrCamera.ViewCamera.transform, false);
        host.transform.localScale = Vector3.one / PixelsPerMetre;
        canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        // Between ordinary HUD and the loading UI. There is no raycaster.
        var topValue = int.MinValue;
        foreach (var layer in SortingLayer.layers)
            if (layer.value > topValue) { topValue = layer.value; canvas.sortingLayerID = layer.id; }
        canvas.sortingOrder = 25000;
        material = new Material(Graphic.defaultGraphicMaterial) { name = "ShipbreakerVr damage UI", renderQueue = 3100 };
        material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        Debug.Log("[ShipbreakerVr] Head-relative damage surface created; stereo coverage enabled; native HUD dimensions retained.");
    }

    private void LateUpdate()
    {
        Restore();
        if (canvas) canvas.enabled = false;
        if (failed || !ModXrManager.IsVrEnabled || !VrCamera.ViewCamera) return;
        try
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (!entry.SourceImage && !entry.SourceRenderer)
                {
                    if (entry.Proxy) Destroy(entry.Proxy.gameObject);
                    entries.RemoveAt(i); continue;
                }
                if (entry.Proxy) entry.Proxy.enabled = false;
                if (entry.SourceImage)
                {
                    var source = entry.SourceImage;
                    if (!source.isActiveAndEnabled || source.color.a <= 0) continue;
                    EnsureCanvas();
                    if (!entry.Proxy) entry.Proxy = NewGraphic<Image>();
                    var image = (Image)entry.Proxy;
                    image.sprite = source.overrideSprite;
                    if (image.sprite && entry.ReportedSprite != image.sprite)
                    {
                        entry.ReportedSprite = image.sprite;
                        Debug.Log($"[ShipbreakerVr] Damage image artwork: source={source.name}; sprite={image.sprite.name}; color={source.color}.");
                    }
                    // Preserve filled fades, but stretch damage vignettes as a
                    // whole: fixed-pixel sliced borders shrink on a far plane.
                    image.type = source.type == Image.Type.Filled ? Image.Type.Filled : Image.Type.Simple;
                    image.fillCenter = source.fillCenter;
                    image.fillAmount = source.fillAmount;
                    image.fillMethod = source.fillMethod;
                    image.fillOrigin = source.fillOrigin;
                    image.fillClockwise = source.fillClockwise;
                    image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                    image.color = source.color * new Color(1, 1, 1, source.canvasRenderer.GetInheritedAlpha());
                    entry.SavedColor = source.color;
                    entry.Suppressed = true;
                    var hidden = source.color; hidden.a = 0; source.color = hidden;
                }
                else
                {
                    var source = entry.SourceRenderer;
                    if (!source.enabled || !source.gameObject.activeInHierarchy) continue;
                    // forceRenderingOff may also be temporarily set by the avatar hider.
                    if (source is SpriteRenderer sprite)
                    {
                        if (!sprite.sprite) continue;
                        EnsureCanvas();
                        if (!entry.Proxy) entry.Proxy = NewGraphic<Image>();
                        var image = (Image)entry.Proxy;
                        image.sprite = sprite.sprite; image.color = sprite.color;
                        image.useSpriteMesh = true;
                    }
                    else
                    {
                        var sourceMaterial = source.sharedMaterial;
                        if (!sourceMaterial || !sourceMaterial.HasProperty("_UnlitColorMap")) continue;
                        var texture = sourceMaterial.GetTexture("_UnlitColorMap");
                        if (!texture) continue;
                        EnsureCanvas();
                        if (!entry.Proxy) entry.Proxy = NewGraphic<RawImage>();
                        var image = (RawImage)entry.Proxy;
                        image.texture = texture;
                        image.color = sourceMaterial.GetColor("_UnlitColor");
                    }
                    entry.SavedHidden = source.forceRenderingOff;
                    entry.Suppressed = true; source.forceRenderingOff = true;
                }
                entry.Proxy.enabled = true; canvas.enabled = true;
            }
            Place();
            PlaceCritical();
        }
        catch (Exception error)
        {
            failed = true; Restore();
            if (canvas) canvas.enabled = false;
            Debug.LogWarning("[ShipbreakerVr] Damage presentation stopped; native effects restored. " + error);
        }
    }
    private T NewGraphic<T>() where T : Graphic
    {
        var host = new GameObject("Damage visual", typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        var graphic = host.AddComponent<T>();
        graphic.raycastTarget = false; graphic.material = material;
        if (graphic is MaskableGraphic maskable) maskable.maskable = false;
        return graphic;
    }
    private void Place()
    {
        var view = VrCamera.ViewCamera;
        if (!canvas || !canvas.enabled || !view) return;
        canvas.worldCamera = view;
        if (canvas.transform.parent != view.transform) canvas.transform.SetParent(view.transform, false);
        canvas.transform.localPosition = Vector3.forward * Distance;
        canvas.transform.localRotation = Quaternion.identity;
        canvas.transform.localScale = Vector3.one / PixelsPerMetre;
        UpdateCoverage(view);
        var size = coverage.size * (1.1f * PixelsPerMetre);
        var center = coverage.center * PixelsPerMetre;
        foreach (var entry in entries)
        {
            if (!entry.Proxy || !entry.Proxy.enabled) continue;
            var rect = entry.Proxy.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = size;
            rect.anchoredPosition3D = new Vector3(center.x, center.y, 0);
            if (entry.SourceRenderer && VrCamera.MainCamera)
            {
                var native = VrCamera.MainCamera;
                var source = entry.SourceRenderer;
                var sprite = source as SpriteRenderer;
                var mesh = sprite ? null : source.GetComponent<MeshFilter>();
                if (!sprite && (!mesh || !mesh.sharedMesh)) { entry.Proxy.enabled = false; continue; }
                var bounds = sprite ? sprite.sprite.bounds : mesh.sharedMesh.bounds;
                var point = native.transform.InverseTransformPoint(source.transform.TransformPoint(bounds.center));
                var localSize = bounds.size;
                var scale = source.transform.lossyScale;
                var width = Mathf.Abs(localSize.x * scale.x);
                var height = Mathf.Abs(localSize.y * scale.y);
                if (!PresentationGeometry.DamageSpriteBounds(point, new Vector2(width, height), Distance, out var nativeBounds))
                { entry.Proxy.enabled = false; continue; }
                rect.sizeDelta = nativeBounds.size * PixelsPerMetre;
                rect.anchoredPosition3D = new Vector3(nativeBounds.center.x * PixelsPerMetre, nativeBounds.center.y * PixelsPerMetre, 0);
                var rotation = Quaternion.Inverse(native.transform.rotation) * source.transform.rotation;
                rect.localRotation = Quaternion.Euler(0, 0, rotation.eulerAngles.z);
                rect.localScale = new Vector3(sprite && sprite.flipX ? -1 : 1, sprite && sprite.flipY ? -1 : 1, 1);
            }
        }
    }
    private void UpdateCoverage(Camera view)
    {
        // HDRP obtains eye matrices from these render parameters, not the legacy
        // Camera.GetStereoViewMatrix/CalculateFrustumCorners path used in 0.4.29.
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var views = 0;
        SubsystemManager.GetInstances(displays);
        foreach (var display in displays)
        {
            if (!display.running) continue;
            for (var passIndex = 0; passIndex < display.GetRenderPassCount(); passIndex++)
            {
                display.GetRenderPass(passIndex, out var pass);
                for (var index = 0; index < pass.GetRenderParameterCount(); index++)
                {
                    pass.GetRenderParameter(view, index, out var parameter);
                    var eyeToHead = view.transform.worldToLocalMatrix * parameter.view.inverse;
                    var eyeMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                    var eyeMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                    var valid = 0;
                    foreach (var corner in corners)
                    {
                        if (!PresentationGeometry.DamageProjectionPoint(parameter.projection, eyeToHead, corner, Distance, out var point)) continue;
                        eyeMin = Vector2.Min(eyeMin, point); eyeMax = Vector2.Max(eyeMax, point); valid++;
                    }
                    if (valid != 4) continue;
                    min = Vector2.Min(min, eyeMin); max = Vector2.Max(max, eyeMax); views++;
                }
            }
        }
        if (views >= 2 && max.x > min.x && max.y > min.y)
        {
            coverage = new Rect(min, max - min); hasCoverage = true;
        }
        // Eye data can be absent during scene/session transitions. Keep the last
        // valid coverage (or a conservative 140-degree plane) and retry, rather
        // than permanently returning to the broken native helmet effects.
        else if (!hasCoverage) coverage = new Rect(-Distance * 2.75f, -Distance * 2.75f, Distance * 5.5f, Distance * 5.5f);
        var status = views >= 2 ? "XR render-pass coverage" : hasCoverage ? "cached coverage; waiting for XR views" : "temporary coverage; waiting for XR views";
        if (status != coverageStatus)
        {
            coverageStatus = status;
            Debug.Log($"[ShipbreakerVr] Damage surface: {status}; views={views}; distance={Distance}; head-parented=True; extent={coverage}.");
        }
    }
    private void SafePlace()
    {
        try { Place(); PlaceCritical(); }
        catch (Exception error)
        {
            failed = true; Restore(); if (canvas) canvas.enabled = false;
            Debug.LogWarning("[ShipbreakerVr] Damage surface placement stopped; native effects restored. " + error);
        }
    }
    private void PlaceCritical()
    {
        if (failed || !ModXrManager.IsVrEnabled || !VrCamera.ViewCamera) return;
        foreach (var visual in critical.Values)
        {
            visual.Restore();
            if (!visual.Active) continue;
            UpdateCoverage(VrCamera.ViewCamera);
            visual.Apply(VrCamera.MainCamera, VrCamera.ViewCamera, coverage, Distance);
        }
    }
    private void Restore()
    {
        foreach (var visual in critical.Values) visual.Restore();
        foreach (var entry in entries)
        {
            if (!entry.Suppressed) continue;
            if (entry.SourceImage) entry.SourceImage.color = entry.SavedColor;
            if (entry.SourceRenderer) entry.SourceRenderer.forceRenderingOff = entry.SavedHidden;
            entry.Suppressed = false;
        }
    }
    private void OnDisable()
    {
        RenderPipelineManager.endFrameRendering -= AfterFrame;
        RenderPipelineManager.beginFrameRendering -= BeforeFrame;
        Canvas.willRenderCanvases -= SafePlace;
        Restore(); if (canvas) canvas.enabled = false;
    }
    private void OnDestroy()
    {
        Restore(); if (canvas) Destroy(canvas.gameObject); if (material) Destroy(material);
        if (instance == this) instance = null;
    }
}

[DefaultExecutionOrder(-11000)]
internal sealed class VrHelmetDamageEarlyRestore : MonoBehaviour
{
    private void Update() => VrHelmetDamage.RestoreEarly();
}

[HarmonyPatch]
internal static class HelmetDamagePatches
{
    internal static void InstallOptional()
    {
        var harmony = new Harmony("ShipbreakerVr.HelmetDamage");
        try { harmony.CreateClassProcessor(typeof(HelmetDamagePatches)).Patch(); }
        catch (Exception error) { harmony.UnpatchSelf(); Debug.LogWarning("[ShipbreakerVr] Helmet damage hooks unavailable; core VR retained. " + error); }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerFXActionVFXBase), "TrySpawnFXObject")]
    private static void Spawned(bool __result, FXElement outObject)
    { if (__result) VrHelmetDamage.RegisterCrack(outObject); }
    [HarmonyPostfix, HarmonyPatch(typeof(FadeScreen), "Awake")]
    private static void FadeCreated(FadeScreen __instance) => VrHelmetDamage.RegisterImage(__instance.FadeScreenImage);
}
