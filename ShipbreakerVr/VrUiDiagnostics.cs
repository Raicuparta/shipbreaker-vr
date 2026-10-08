using System.Collections.Generic;
using BBI.Unity.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ShipbreakerVr;

internal static class VrUiDiagnostics
{
    // A user-triggered snapshot of rendering state, not screenshots or menu inputs.
    public static void Capture()
    {
        var view = VrCamera.ViewCamera;
        Debug.Log($"[ShipbreakerVr] UI SNAPSHOT BEGIN state={GameSession.CurrentGameState}; vr={ModXrManager.IsVrEnabled}; focused={Application.isFocused}; view={(view ? view.name : "missing")}; frame={Time.frameCount}");
        foreach (var camera in Object.FindObjectsOfType<Camera>())
        {
            Debug.Log($"[ShipbreakerVr] UI camera {camera.name}: enabled={camera.enabled}; active={camera.gameObject.activeInHierarchy}; stereo={camera.stereoEnabled}/{camera.stereoTargetEye}; depth={camera.depth}; clip={camera.nearClipPlane}/{camera.farClipPlane}; mask={camera.cullingMask}; target={(camera.targetTexture ? camera.targetTexture.name : "display")}; position={camera.transform.position}; forward={camera.transform.forward}");
            var hd = camera.GetComponent<HDAdditionalCameraData>();
            Debug.Log(hd
                ? $"[ShipbreakerVr] UI camera HDRP {camera.name}: requestedXR={hd.xrRendering}; volumeMask={hd.volumeLayerMask.value}; anchor={(hd.volumeAnchorOverride ? hd.volumeAnchorOverride.name : "camera")}; customFrameSettings={hd.customRenderingSettings}; defaults={hd.defaultFrameSettings}; clear={hd.clearColorMode}; AA={hd.antialiasing}"
                : $"[ShipbreakerVr] UI camera HDRP {camera.name}: no explicit additional camera data");
        }
        foreach (var volume in Object.FindObjectsOfType<Volume>())
        {
            var profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
            var effects = new List<string>();
            if (profile)
                foreach (var component in profile.components)
                    if (component && component.active) effects.Add(component.GetType().Name);
            Debug.Log($"[ShipbreakerVr] UI volume {volume.name}: enabled={volume.enabled}; layer={volume.gameObject.layer}; global={volume.isGlobal}; weight={volume.weight}; priority={volume.priority}; effects={string.Join(",", effects)}");
        }
        foreach (var canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas) continue;
            var rect = canvas.transform as RectTransform;
            var center = rect ? rect.TransformPoint(rect.rect.center) : canvas.transform.position;
            var viewport = view ? view.WorldToViewportPoint(center) : Vector3.zero;
            var renderers = canvas.GetComponentsInChildren<CanvasRenderer>(false);
            var culled = 0;
            var transparent = 0;
            var materialSlots = 0;
            var materialSummary = new HashSet<string>();
            foreach (var renderer in renderers)
            {
                if (renderer.cull) culled++;
                if (renderer.GetInheritedAlpha() <= 0.001f) transparent++;
                materialSlots += renderer.materialCount;
                for (var slot = 0; slot < renderer.materialCount; slot++)
                {
                    var material = renderer.GetMaterial(slot);
                    if (material) materialSummary.Add($"{(material.shader ? material.shader.name : "no shader")}@{material.renderQueue}");
                }
            }
            Debug.Log($"[ShipbreakerVr] UI canvas {canvas.name}: enabled={canvas.enabled}; mode={canvas.renderMode}; camera={(canvas.worldCamera ? canvas.worldCamera.name : "none")}; sort={canvas.sortingLayerID}/{canvas.sortingOrder}; size={(rect ? rect.rect.size : Vector2.zero)}; scale={canvas.transform.lossyScale.ToString("F5")}; viewCenter={viewport}; renderers={renderers.Length}; culled={culled}; alphaZero={transparent}; slots={materialSlots}; materials={string.Join(",", materialSummary)}");
        }
        Debug.Log("[ShipbreakerVr] UI SNAPSHOT END");
    }
}
