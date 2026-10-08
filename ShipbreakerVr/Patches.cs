using HarmonyLib;
using UnityEngine;
using BBI.Unity.Game;
using UnityEngine.UI;

namespace ShipbreakerVr;

[HarmonyPatch]
public static class Patches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Hab3DController), "Awake")]
    private static void RegisterHabitatCamera(Hab3DController __instance) => VrCamera.RegisterHabitat(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LynxCameraController), "OnGameStateChangedEvent")]
    private static void ReleaseCameraBeforeStateChange() => VrCamera.BeforeGameCameraStateChange();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LynxCameraController), "Start")]
    private static void CreateVrCamera(LynxCameraController __instance)
    {
        var camera = LynxCameraController.MainCamera;
        if (!camera) camera = __instance.GetComponent<Camera>();
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

        if (!canvas || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;

        VrUi.Attach(canvas);
    }
}
