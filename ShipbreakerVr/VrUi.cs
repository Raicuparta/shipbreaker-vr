using System.Collections.Generic;
using BBI.Unity.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ShipbreakerVr;

[DefaultExecutionOrder(9100)]
public class VrUi : MonoBehaviour
{
    private static readonly Dictionary<int, VrUi> Controllers = new Dictionary<int, VrUi>();
    private const float ForwardOffset = 1.3f;
    private int canvasId;
    private Canvas canvas;
    private CanvasScaler scaler;
    private CanvasScalerImprover improver;
    private bool scalerEnabled;
    private bool improverEnabled;
    private bool applied;
    private bool transformApplied;
    private Vector3 initialPosition;
    private RenderMode initialRenderMode;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private Transform target;
    private Camera initialCamera;
    private RectTransform rect;
    private Vector2 initialSizeDelta;
    private Vector2 layoutSize;
    private Vector3 initialAnchoredPosition;
    private int visibleLayers;
    private readonly VrFrontendClip frontendClip = new VrFrontendClip();
    private readonly VrUiMaterials materials = new VrUiMaterials();
    private readonly VrHudCurve curve = new VrHudCurve();
    private readonly VrUiSorting sorting = new VrUiSorting();
    private VrLoadingBackdrop loadingBackdrop;
    private bool backdropFailed;

    private void OnEnable()
    {
        // Register after Unity's UI rebuild callback so stencil/TMP materials exist first.
        _ = CanvasUpdateRegistry.instance;
        Canvas.preWillRenderCanvases += curve.Restore;
        Canvas.willRenderCanvases += PrepareMaterials;
    }

    private void PrepareMaterials()
    {
        if (applied && canvas && canvas.isActiveAndEnabled && ModXrManager.IsVrEnabled)
        {
            ApplyPlacement();
            using (new VrUiPerformance.Scope(VrUiPerformance.Work.Sorting)) sorting.Apply(canvas);
            loadingBackdrop?.SynchronizeOrder();
            using (new VrUiPerformance.Scope(VrUiPerformance.Work.Materials)) materials.Apply(canvas);
            using (new VrUiPerformance.Scope(VrUiPerformance.Work.Clipping)) frontendClip.Apply(canvas);
            using (new VrUiPerformance.Scope(VrUiPerformance.Work.RoomMarkers)) VrRoomMarkers.Prepare(canvas);
            if (transformApplied) curve.Apply(canvas);
        }
    }

    public static int VisibleLayerMask
    {
        get
        {
            var mask = 0;
            foreach (var follower in Controllers.Values)
                if (follower && follower.applied && follower.canvas && follower.canvas.isActiveAndEnabled)
                    mask |= follower.visibleLayers;
            return mask;
        }
    }

    public static void Attach(Canvas canvas)
    {
        if (!canvas || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;
        var id = canvas.GetInstanceID();
        if (Controllers.TryGetValue(id, out var existing) && existing) return;
        VrHelmetDamage.RegisterCanvas(canvas);
        // The game converts canvas GameObjects into Entities. Adding an unknown mod
        // component to those objects makes that conversion throw; own the behaviour separately.
        var host = new GameObject("ShipbreakerVr UI follower");
        host.SetActive(false);
        DontDestroyOnLoad(host);
        var follower = host.AddComponent<VrUi>();
        follower.canvas = canvas;
        follower.canvasId = id;
        Controllers[id] = follower;
        host.SetActive(true);
        if (ModXrManager.IsVrEnabled && VrCamera.BodyTransform && VrCamera.ViewCamera && canvas.isActiveAndEnabled)
            follower.SetUpCanvas();
    }

    private void Update()
    {
        if (!canvas) { Destroy(gameObject); return; }
        var shouldApply = ModXrManager.IsVrEnabled && canvas.isActiveAndEnabled && canvas.isRootCanvas && VrCamera.BodyTransform && VrCamera.ViewCamera;
        if (shouldApply && !applied) SetUpCanvas();
        else if (!shouldApply && applied) ResetCanvas();
    }

    private void LateUpdate()
    {
        if (!canvas || !applied) return;
        target = VrCamera.BodyTransform;
        if (!target) return;
        canvas.worldCamera = VrCamera.ViewCamera;
        if (!backdropFailed && canvas.name == "LoadScreenCanvas" && initialCamera && initialCamera.name == "LoadScreenCamera")
        {
            try
            {
                if (loadingBackdrop == null) loadingBackdrop = new VrLoadingBackdrop(initialCamera, canvas);
                loadingBackdrop.Update();
            }
            catch (System.Exception exception)
            {
                loadingBackdrop?.Dispose();
                loadingBackdrop = null;
                backdropFailed = true;
                Debug.LogError($"[ShipbreakerVr] Loading backdrop unavailable; readable UI retained. {exception}");
            }
        }
        ApplyPlacement();
    }

    private void ApplyPlacement()
    {
        using var measurement = new VrUiPerformance.Scope(VrUiPerformance.Work.Placement);
        if (!transformApplied || !canvas) return;
        var isLoading = canvas.name == "LoadScreenCanvas";
        var followsHead = isLoading || VrHudCurve.FollowsHead(canvas);
        // Loading text and its static backdrop share one head-relative reference.
        // Camera creation/replacement and loading animations cannot change their separation.
        var anchor = followsHead && VrCamera.ViewCamera ? VrCamera.ViewCamera.transform : VrCamera.BodyTransform;
        if (!anchor) return;
        // Habitat transitions also re-enable scalers and animate root transforms.
        // Reassert the owned root layout, while leaving child menu animations intact.
        if (transformApplied)
        {
            if (canvas.renderMode != RenderMode.WorldSpace) canvas.renderMode = RenderMode.WorldSpace;
            if (canvas.worldCamera != VrCamera.ViewCamera) canvas.worldCamera = VrCamera.ViewCamera;
            SetBehaviourEnabled(improver, false);
            SetBehaviourEnabled(scaler, false);
            if (rect)
            {
                var size = rect.rect.size;
                var height = layoutSize.y * VrHudCurve.VerticalLayoutScale(canvas);
                if (size.x != layoutSize.x) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, layoutSize.x);
                if (size.y != height) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
            SetPanelScale(layoutSize.x);
        }
        // Place the visual center ahead of the body even for non-centered pivots.
        var center = rect ? rect.rect.center : Vector2.zero;
        var offset = new Vector3(center.x * canvas.transform.lossyScale.x, center.y * canvas.transform.lossyScale.y, 0f);
        var distance = followsHead ? ForwardOffset : ForwardOffset + VrCamera.EyeOffset.z;
        canvas.transform.position = anchor.position + anchor.forward * distance - anchor.rotation * offset;
        canvas.transform.rotation = anchor.rotation;
    }

    private void SetUpCanvas()
    {
        // A previously registered screen can become world-space during a menu animation.
        // Give that screen depth protection without moving it or disabling its scaler.
        if (!canvas.isRootCanvas) return;
        target = VrCamera.BodyTransform;
        if (!target) return;
        initialPosition = canvas.transform.position;
        initialRotation = canvas.transform.rotation;
        initialScale = canvas.transform.localScale;
        initialRenderMode = canvas.renderMode;
        initialCamera = canvas.worldCamera;
        transformApplied = initialRenderMode != RenderMode.WorldSpace;
        if (!transformApplied)
        {
            canvas.worldCamera = VrCamera.ViewCamera;
            CaptureLayers();
            applied = true;
            Debug.Log($"[ShipbreakerVr] UI depth-only panel: {canvas.name}; native world-space layout preserved; sort={canvas.sortingLayerID}/{canvas.sortingOrder}");
            return;
        }
        rect = canvas.transform as RectTransform;
        initialSizeDelta = rect ? rect.sizeDelta : Vector2.zero;
        initialAnchoredPosition = rect ? rect.anchoredPosition3D : Vector3.zero;
        var size = rect ? rect.rect.size : new Vector2(1920f, 1080f);
        layoutSize = size;
        scaler = canvas.GetComponent<CanvasScaler>();
        improver = canvas.GetComponent<CanvasScalerImprover>();
        scalerEnabled = scaler && scaler.enabled;
        improverEnabled = improver && improver.enabled;
        SetBehaviourEnabled(improver, false);
        SetBehaviourEnabled(scaler, false);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = VrCamera.ViewCamera;
        if (rect)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * VrHudCurve.VerticalLayoutScale(canvas));
        }
        // Keep a stable two-metre panel rather than depending on desktop resolution.
        SetPanelScale(size.x);
        CaptureLayers();
        applied = true;
        curve.Register(canvas);
        ApplyPlacement();
        Debug.Log($"[ShipbreakerVr] UI panel: {canvas.name}; original={initialRenderMode}; pixels={size}; layers={visibleLayers}; sort={canvas.sortingLayerID}/{canvas.sortingOrder}; source={target.name}");
    }

    private void SetPanelScale(float width)
    {
        var scale = width > 0f ? VrHudCurve.PanelWidth(canvas) / width : 0.001f;
        var parentScale = canvas.transform.parent ? canvas.transform.parent.lossyScale : Vector3.one;
        var desiredScale = new Vector3(
            scale / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
            scale / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
            scale / Mathf.Max(Mathf.Abs(parentScale.z), 0.0001f));
        if (!canvas.transform.localScale.Equals(desiredScale)) canvas.transform.localScale = desiredScale;
    }

    private void ResetCanvas()
    {
        frontendClip.Restore();
        sorting.Restore();
        curve.Dispose();
        loadingBackdrop?.Dispose();
        loadingBackdrop = null;
        backdropFailed = false;
        materials.Restore();
        applied = false;
        if (!canvas) return;
        canvas.worldCamera = initialCamera;
        if (transformApplied)
        {
            canvas.renderMode = initialRenderMode;
            if (rect) { rect.sizeDelta = initialSizeDelta; rect.anchoredPosition3D = initialAnchoredPosition; }
            canvas.transform.position = initialPosition;
            canvas.transform.rotation = initialRotation;
            canvas.transform.localScale = initialScale;
            SetBehaviourEnabled(scaler, scalerEnabled);
            SetBehaviourEnabled(improver, improverEnabled);
        }
        transformApplied = false;
        target = null;
        applied = false;
    }

    private void OnDisable()
    {
        frontendClip.Restore();
        sorting.Restore();
        Canvas.preWillRenderCanvases -= curve.Restore;
        Canvas.willRenderCanvases -= PrepareMaterials;
        curve.Dispose();
        if (applied) ResetCanvas();
        else materials.Restore();
    }

    private void CaptureLayers()
    {
        visibleLayers = 1 << canvas.gameObject.layer;
        foreach (var renderer in canvas.GetComponentsInChildren<CanvasRenderer>(true))
            visibleLayers |= 1 << renderer.gameObject.layer;
    }

    private void OnDestroy()
    {
        if (Controllers.TryGetValue(canvasId, out var current) && current == this) Controllers.Remove(canvasId);
    }

    private static void SetBehaviourEnabled(MonoBehaviour behaviour, bool enabled)
    {
        if (!behaviour) return;
        if (behaviour.enabled != enabled) behaviour.enabled = enabled;
    }
}
