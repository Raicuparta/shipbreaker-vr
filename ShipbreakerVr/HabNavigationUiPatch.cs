using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[HarmonyPatch(typeof(Hab3DLocationNavigation), "UpdateNavLocation")]
internal static class HabNavigationUiPatch
{
    private static bool Prefix(NavigationOption nav, Hab3DController ___mHab3DController)
    {
        if (!ModXrManager.IsVrEnabled || !___mHab3DController || nav == null || !nav.ClickableButton || !nav.TransformToTrack) return true;
        var button = nav.ClickableButton.RectTransform;
        var panel = button.parent && button.parent.parent ? button.parent.parent.GetComponent<RectTransform>() : null;
        var camera = ___mHab3DController.CinemachineBrain.OutputCamera;
        if (!panel || !camera) return true;
        var point = camera.WorldToViewportPoint(nav.TransformToTrack.position, Camera.MonoOrStereoscopicEye.Mono);
        // ScreenPoint.z is world depth, not a UI pixel coordinate. It must never
        // move the prompt behind the panel when that panel becomes world-space.
        button.localPosition = new Vector3((point.x - .5f) * panel.rect.width, (point.y - .5f) * panel.rect.height, 0f);
        return false;
    }
}
