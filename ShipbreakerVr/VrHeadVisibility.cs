using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

// Hide only registered separate helmet renderers in the local avatar.
// Render-time overrides leave the shared model, damage systems and UI untouched.
internal sealed class VrHeadVisibility
{
    private static ConfigEntry<bool> hide;
    private static readonly Dictionary<HelmetController, Renderer[]> helmetMeshes = new Dictionary<HelmetController, Renderer[]>();
    private readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
    internal static void Register(HelmetController controller)
    {
        var stale = new List<HelmetController>();
        foreach (var pair in helmetMeshes) if (!pair.Key) stale.Add(pair.Key);
        foreach (var key in stale) helmetMeshes.Remove(key);
        var helmet = controller.CurrentHelment;
        helmetMeshes[controller] = helmet && helmet.MeshGameObject ? helmet.MeshGameObject.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        Debug.Log($"[ShipbreakerVr] Helmet mesh registered: controller={controller.name}; renderers={helmetMeshes[controller].Length}");
    }
    internal static void Configure(ConfigFile config) => hide = config.Bind("Avatar", "HideHelmetInVr", true,
        "Hide the local head/helmet during VR rendering; suit readouts, damage logic and flat view are preserved.");
    internal void Apply(Animator animator)
    {
        if (!hide.Value) return;
        foreach (var pair in helmetMeshes)
            if (pair.Key) foreach (var renderer in pair.Value)
                if (renderer && !hidden.ContainsKey(renderer)) { hidden[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
    }
    internal void Restore()
    {
        foreach (var pair in hidden) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
        hidden.Clear();
    }
    internal void Clear() => Restore();
}

[HarmonyPatch]
internal static class HelmetVisualPatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(HelmetController), "Awake")]
    private static void Created(HelmetController __instance) => VrHeadVisibility.Register(__instance);
    [HarmonyPostfix, HarmonyPatch(typeof(HelmetController), "SwapHelmet")]
    private static void Swapped(HelmetController __instance) => VrHeadVisibility.Register(__instance);
}
