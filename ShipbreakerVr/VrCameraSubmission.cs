using System.Collections.Generic;
using ShipbreakerVr.Tracking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ShipbreakerVr;

// HDRP checks xrRendering after beginFrameRendering, when constructing its XR passes.
// Other game cameras keep drawing to the desktop/textures without owning the HMD.
internal sealed class VrCameraSubmission : MonoBehaviour
{
    private readonly TemporaryBooleanOverrides<HDAdditionalCameraData> flags =
        new TemporaryBooleanOverrides<HDAdditionalCameraData>(
            data => data && data.xrRendering,
            (data, value) => { if (data) data.xrRendering = value; });
    private readonly HashSet<int> announced = new HashSet<int>();

    private void OnEnable()
    {
        RenderPipelineManager.beginFrameRendering += BeforeFrame;
        RenderPipelineManager.endFrameRendering += AfterFrame;
    }

    private void BeforeFrame(ScriptableRenderContext context, Camera[] cameras)
    {
        // Recover as well if the preceding render did not reach its end callback.
        flags.Restore();
        var view = VrCamera.ViewCamera;
        if (!ModXrManager.IsSessionRunning)
        {
            announced.Clear();
            return;
        }
        foreach (var camera in cameras)
        {
            if (!camera || (camera == view && ModXrManager.IsVrEnabled) || camera.cameraType != CameraType.Game || camera.targetTexture) continue;
            var data = camera.GetComponent<HDAdditionalCameraData>();
            if (!data)
            {
                if (announced.Add(camera.GetInstanceID()))
                    Debug.LogWarning($"[ShipbreakerVr] Extra display camera lacks HDRP data; XR submission unchanged: {camera.name}");
                continue;
            }
            if (flags.Suppress(data) && announced.Add(camera.GetInstanceID()))
                Debug.Log($"[ShipbreakerVr] XR view ownership: {camera.name} excluded from headset for this render; depth={camera.depth}; VrCamera owns headset; desktop rendering preserved");
        }
    }

    private void AfterFrame(ScriptableRenderContext context, Camera[] cameras) => flags.Restore();

    private void OnDisable()
    {
        RenderPipelineManager.beginFrameRendering -= BeforeFrame;
        RenderPipelineManager.endFrameRendering -= AfterFrame;
        flags.Restore();
        announced.Clear();
    }
}
