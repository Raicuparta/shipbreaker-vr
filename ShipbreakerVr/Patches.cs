using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ShipbreakerVr;

[HarmonyPatch]
public static class Patches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LynxCameraController), nameof(LynxCameraController.Start))]
    private static void CreateVrCamera(LynxCameraController __instance)
    {
        var camera = __instance.GetComponent<Camera>();
        if (!camera)
        {
            Debug.LogError("Couldn't find Main Camera in LynxCameraController");
            return;
        }

        VrCamera.Create(camera);
    }

    private static void Disable(MonoBehaviour component)
    {
        if (!component) return;
        component.enabled = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
    private static void MoveCanvasesToWorldSpace(CanvasScaler __instance)
    {
        var canvas = __instance.GetComponent<Canvas>();

        if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;

        var attach = canvas.gameObject.AddComponent<AttachToTarget>();
        attach.Target = Camera.main.transform;
        attach.ForwardOffset = 3f;
        canvas.transform.localScale = Vector3.one * 0.0025f;
        canvas.renderMode = RenderMode.WorldSpace;
        MaterialHelper.MakeGraphicChildrenDrawOnTop(canvas.gameObject);
        Disable(canvas.GetComponent<CanvasScalerImprover>());
        Disable(canvas.GetComponent<CanvasScaler>());
    }
}