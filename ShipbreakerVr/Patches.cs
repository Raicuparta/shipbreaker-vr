using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ShipbreakerVr;

[HarmonyPatch]
public static class Patches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LynxCameraController), nameof(LynxCameraController.Start))]
    private static void CreateVrCamera(LynxCameraController instance)
    {
        var camera = instance.GetComponent<Camera>();
        if (!camera)
        {
            Debug.LogError("Couldn't find Main Camera in LynxCameraController");
            return;
        }

        VrCamera.Create(camera);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
    private static void MoveCanvasesToWorldSpace(CanvasScaler instance)
    {
        var canvas = instance.GetComponent<Canvas>();

        if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;

        canvas.gameObject.AddComponent<VrUi>();
    }
}