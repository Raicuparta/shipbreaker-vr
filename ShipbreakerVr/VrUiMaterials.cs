using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipbreakerVr;

// Operate on the final CanvasRenderer materials, after Unity/TMP have applied
// font atlases and stencil masks. Never edit shared game or font materials.
internal sealed class VrUiMaterials
{
    private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>();
    private readonly HashSet<CanvasRenderer> touched = new HashSet<CanvasRenderer>();
    private readonly List<CanvasRenderer> renderers = new List<CanvasRenderer>();
    private readonly HashSet<Material> refreshed = new HashSet<Material>();
    private readonly HashSet<string> loggedShaders = new HashSet<string>();

    public void Apply(Canvas canvas)
    {
        refreshed.Clear();
        renderers.Clear();
        canvas.GetComponentsInChildren(false, renderers);
        foreach (var renderer in renderers)
        {
            if (!renderer) continue;
            touched.Add(renderer);
            for (var slot = 0; slot < renderer.materialCount; slot++)
            {
                var material = renderer.GetMaterial(slot);
                var overlay = GetOverlay(material);
                if (overlay && overlay != material) renderer.SetMaterial(overlay, slot);
            }
            // Keep stencil pop/clear passes paired with their masked drawing passes.
            for (var slot = 0; slot < renderer.popMaterialCount; slot++)
            {
                var material = renderer.GetPopMaterial(slot);
                var overlay = GetOverlay(material);
                if (overlay && overlay != material) renderer.SetPopMaterial(overlay, slot);
            }
        }
    }

    private Material GetOverlay(Material material)
    {
        if (!material) return null;
        var source = originals.TryGetValue(material, out var original) ? original : material;
        if (!source) return null;
        if (!copies.TryGetValue(source, out var overlay) || !overlay)
        {
            overlay = new Material(source) { name = source.name + " (ShipbreakerVr UI)", hideFlags = HideFlags.HideAndDontSave };
            copies[source] = overlay;
            originals[overlay] = source;
        }
        if (refreshed.Add(source))
        {
            // Preserve changing colors, textures, keywords, clipping and stencil values.
            if (overlay.shader != source.shader) overlay.shader = source.shader;
            overlay.CopyPropertiesFromMaterial(source);
            // Unity UI and TMP use this render-state uniform even if it is not in Properties.
            overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            if (overlay.HasProperty("_ZTest")) overlay.SetInt("_ZTest", (int)CompareFunction.Always);
            if (overlay.HasProperty("_ZTestMode")) overlay.SetInt("_ZTestMode", (int)CompareFunction.Always);
            if (overlay.HasProperty("_ZWrite")) overlay.SetInt("_ZWrite", 0);
            // HDRP 10's world-space transparent passes exclude Overlay (4000).
            // 3100 is HDRP 10's inclusive TransparentLast: draw UI after ordinary
            // transparent/scanner geometry as well as ignoring opaque depth. Keep
            // stencil drawing and pop passes together in the same supported pass.
            overlay.renderQueue = 3100;
            var shaderName = source.shader ? source.shader.name : "missing";
            if (loggedShaders.Add(shaderName))
                Debug.Log($"[ShipbreakerVr] UI depth material: shader={shaderName}; sourceQueue={source.renderQueue}; effectiveQueue={overlay.renderQueue}; stencil/clip properties preserved");
        }
        return overlay;
    }

    public void Restore()
    {
        foreach (var renderer in touched)
        {
            if (!renderer) continue;
            for (var slot = 0; slot < renderer.materialCount; slot++)
            {
                var material = renderer.GetMaterial(slot);
                if (material && originals.TryGetValue(material, out var original)) renderer.SetMaterial(original, slot);
            }
            for (var slot = 0; slot < renderer.popMaterialCount; slot++)
            {
                var material = renderer.GetPopMaterial(slot);
                if (material && originals.TryGetValue(material, out var original)) renderer.SetPopMaterial(original, slot);
            }
        }
        foreach (var material in copies.Values) if (material) Object.Destroy(material);
        copies.Clear();
        originals.Clear();
        touched.Clear();
        renderers.Clear();
        refreshed.Clear();
    }
}
