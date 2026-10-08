using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace ShipbreakerVr;

[HarmonyPatch(typeof(LynxCameraController), "UpdateScreenDimensions")]
internal static class ScreenDimensionsPatch
{
    private static readonly System.Reflection.FieldInfo width = AccessTools.Field(typeof(LynxCameraController), "<ScreenWidth>k__BackingField");
    private static readonly System.Reflection.FieldInfo height = AccessTools.Field(typeof(LynxCameraController), "<ScreenHeight>k__BackingField");
    private static readonly System.Reflection.FieldInfo center = AccessTools.Field(typeof(LynxCameraController), "<ScreenCenter>k__BackingField");
    private static void Postfix()
    {
        if (!ModXrManager.IsSessionRunning) return;
        // The native cache only notices desktop resolution changes, not VR view toggles.
        var w = ModXrManager.IsVrEnabled ? XRSettings.eyeTextureWidth : Screen.width;
        var h = ModXrManager.IsVrEnabled ? XRSettings.eyeTextureHeight : Screen.height;
        if (w <= 0 || h <= 0 || (w == LynxCameraController.ScreenWidth && h == LynxCameraController.ScreenHeight)) return;
        width.SetValue(null, w); height.SetValue(null, h); center.SetValue(null, new Vector2(w * .5f, h * .5f));
    }
}
