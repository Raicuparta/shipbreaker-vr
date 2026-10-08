using ShipbreakerVr.Tracking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ShipbreakerVr;

// Use the same world-space UI shader path as the working HUD/damage surfaces.
// This is a visual-only marker: no raycaster, input device or gameplay mutation.
internal sealed class VrCouchReticle
{
    private Canvas canvas;
    private CouchReticleGraphic ring;
    private Material material;
    private bool reported;

    internal void Draw(Transform owner, Camera view, Vector3 target, Color color)
    {
        if (!canvas)
        {
            var host = new GameObject("ShipbreakerVr couch reticle", typeof(RectTransform), typeof(Canvas));
            host.SetActive(false);
            host.transform.SetParent(owner, false);
            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 24000; // above ordinary HUD; below helmet damage/loading
            var topValue = int.MinValue;
            foreach (var layer in SortingLayer.layers)
                if (layer.value > topValue) { topValue = layer.value; canvas.sortingLayerID = layer.id; }
            ((RectTransform)host.transform).sizeDelta = Vector2.one * 2f;
            material = new Material(Graphic.defaultGraphicMaterial) { name = "ShipbreakerVr couch reticle UI", renderQueue = 3100 };
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            var graphic = new GameObject("Aim ring", typeof(RectTransform), typeof(CanvasRenderer));
            graphic.transform.SetParent(host.transform, false);
            ring = graphic.AddComponent<CouchReticleGraphic>();
            ring.rectTransform.sizeDelta = Vector2.one * 2f;
            ring.raycastTarget = false;
            ring.material = material;
            host.SetActive(true);
            Debug.Log("[ShipbreakerVr] Couch reticle UI surface created; native right-stick aim, no input/raycaster.");
        }
        var mask = view.cullingMask;
        var visibleLayer = 0;
        while (visibleLayer < 31 && (mask & (1 << visibleLayer)) == 0) visibleLayer++;
        canvas.gameObject.layer = ring.gameObject.layer = visibleLayer;
        canvas.worldCamera = view;
        var visiblePoint = PresentationGeometry.CouchMarkerPosition(view.transform.position, target, view.nearClipPlane, view.farClipPlane);
        var size = PresentationGeometry.CouchMarkerSize(Vector3.Distance(visiblePoint, view.transform.position));
        canvas.transform.SetPositionAndRotation(visiblePoint, view.transform.rotation);
        canvas.transform.localScale = Vector3.one * size.x;
        ring.color = color;
        ring.SetStroke(size.y / size.x);
        canvas.enabled = true;
        if (!reported)
        {
            reported = true;
            Debug.Log($"[ShipbreakerVr] Couch UI reticle drawing: target={Vector3.Distance(target, view.transform.position):F2}m; visible={Vector3.Distance(visiblePoint, view.transform.position):F2}m; clip={view.nearClipPlane:F2}/{view.farClipPlane:F2}; layer={visibleLayer}; radius={size.x:F3}m");
        }
    }

    internal void Hide() { if (canvas) canvas.enabled = false; }
    internal void Dispose()
    {
        if (canvas) Object.Destroy(canvas.gameObject);
        if (material) Object.Destroy(material);
    }
}

internal sealed class CouchReticleGraphic : MaskableGraphic
{
    private float stroke = .225f;
    internal void SetStroke(float value)
    {
        if (Mathf.Abs(stroke - value) < .0001f) return;
        stroke = value;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int segments = 32;
        var inner = Mathf.Clamp(1f - stroke, .1f, .95f);
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            var point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            mesh.AddVert(point, color, Vector2.zero);
            mesh.AddVert(point * inner, color, Vector2.zero);
            if (i == 0) continue;
            var start = (i - 1) * 2;
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 1, start + 3, start + 2);
        }
    }
}
