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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
    private static void MoveCanvasesToWorldSpace(CanvasScaler __instance)
    {
        var canvas = __instance.GetComponent<Canvas>();

        if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;

        canvas.gameObject.AddComponent<VrUi>();
    }
}