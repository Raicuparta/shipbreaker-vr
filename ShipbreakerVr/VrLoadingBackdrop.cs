using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// Adapt the game's loading-camera effects into a mono texture behind stereo UI.
// The original camera, volume profiles and desktop output remain game-owned.
internal sealed class VrLoadingBackdrop : IDisposable
{
    private readonly Camera source;
    private readonly Canvas loadingUi;
    private Canvas panelCanvas;
    private GameObject cameraHost;
    private GameObject panelHost;
    private Camera capture;
    private HDAdditionalCameraData captureData;
    private RenderTexture texture;
    private Material material;
    private bool visible;

    public VrLoadingBackdrop(Camera source, Canvas loadingUi)
    {
        this.source = source;
        this.loadingUi = loadingUi;
    }

    public void Update()
    {
        var view = VrCamera.ViewCamera;
        var active = ModXrManager.IsVrEnabled && view && view.isActiveAndEnabled &&
            source && source.isActiveAndEnabled && loadingUi && loadingUi.isActiveAndEnabled;
        var sourceData = source ? source.GetComponent<HDAdditionalCameraData>() : null;
        active &= sourceData != null;
        if (active && !capture) Create(view);
        if (capture)
        {
            capture.enabled = active;
            panelHost.SetActive(active);
        }
        SynchronizeOrder();
        if (!active) { visible = false; return; }

        capture.CopyFrom(source);
        capture.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        sourceData.CopyTo(captureData);
        captureData.xrRendering = false;
        // No game meshes or text in the effect texture: only the original clear and post effects.
        capture.cullingMask = 0;
        capture.targetTexture = texture;
        capture.stereoTargetEye = StereoTargetEyeMask.None;
        capture.rect = new Rect(0f, 0f, 1f, 1f);
        capture.aspect = (float)texture.width / texture.height;
        capture.depth = view.depth - 100f;
        capture.enabled = true;
        if (!visible)
        {
            visible = true;
            Debug.Log($"[ShipbreakerVr] Loading backdrop active: source={source.name}; volumeMask={captureData.volumeLayerMask.value}; backdrop order={PresentationGeometry.LoadingBackdropOrder}; loading text reserved above backdrop; queues=2999/3100");
        }
    }

    internal void SynchronizeOrder()
    {
        if (!panelCanvas || !loadingUi) return;
        // The shared UI sorter owns text order; the backdrop never overwrites it.
        panelCanvas.sortingLayerID = loadingUi.sortingLayerID;
        panelCanvas.sortingOrder = PresentationGeometry.LoadingBackdropOrder;
    }

    private void Create(Camera view)
    {
        // A texture without alpha stays opaque even when the game's clear color has alpha zero.
        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
            ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.RGB565;
        texture = new RenderTexture(1536, 864, 24, format)
        {
            name = "ShipbreakerVr loading effects", hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        if (!texture.Create()) throw new InvalidOperationException("Could not create loading backdrop texture.");
        var previous = RenderTexture.active;
        try { RenderTexture.active = texture; GL.Clear(true, true, Color.black); }
        finally { RenderTexture.active = previous; }

        cameraHost = new GameObject("ShipbreakerVr loading effects camera");
        UnityEngine.Object.DontDestroyOnLoad(cameraHost);
        capture = cameraHost.AddComponent<Camera>();
        capture.enabled = false;
        captureData = cameraHost.AddComponent<HDAdditionalCameraData>();

        panelHost = new GameObject("ShipbreakerVr loading backdrop", typeof(RectTransform));
        panelHost.layer = loadingUi.gameObject.layer;
        panelHost.transform.SetParent(view.transform, false);
        // Follow head rotation, covering the view even while looking away from the body-facing text.
        panelHost.transform.localPosition = new Vector3(0f, 0f, 2f);
        ((RectTransform)panelHost.transform).sizeDelta = new Vector2(20f, 20f);
        var canvas = panelHost.AddComponent<Canvas>();
        panelCanvas = canvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = view;
        canvas.sortingLayerID = loadingUi.sortingLayerID;
        canvas.sortingOrder = PresentationGeometry.LoadingBackdropOrder;
        material = new Material(Graphic.defaultGraphicMaterial)
        {
            name = "ShipbreakerVr loading backdrop UI", hideFlags = HideFlags.HideAndDontSave
        };
        material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        // Both queues remain in HDRP's transparent range. Sorting on child canvases
        // cannot put this static image over the text, even during loading animations.
        material.renderQueue = 2999;
        var image = panelHost.AddComponent<RawImage>();
        image.material = material;
        image.texture = texture;
        image.raycastTarget = false;
    }

    public void Dispose()
    {
        if (capture) { capture.enabled = false; capture.targetTexture = null; }
        if (panelHost) { panelHost.SetActive(false); UnityEngine.Object.Destroy(panelHost); }
        if (cameraHost) UnityEngine.Object.Destroy(cameraHost);
        if (material) UnityEngine.Object.Destroy(material);
        if (texture) { texture.Release(); UnityEngine.Object.Destroy(texture); }
    }
}
