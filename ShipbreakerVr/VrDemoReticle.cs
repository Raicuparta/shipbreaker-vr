using BBI.Unity.Game;
using BBI.Unity.Game.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ShipbreakerVr;

// Lives on a mod-owned host, never a game entity/blueprint.
[DefaultExecutionOrder(9500)]
internal sealed class VrDemoReticle : MonoBehaviour
{
    private DemoChargeUIController owner;
    private Transform[] reticles;
    private Quaternion rotation = Quaternion.identity;
    private bool wasVr;
    private static VrDemoReticle instance;
    internal static void Register(DemoChargeUIController value, Image a, Image b, Image c)
    {
        if (!instance) instance = new GameObject("ShipbreakerVr demo reticle").AddComponent<VrDemoReticle>();
        instance.owner = value; instance.reticles = new[] { a.transform, b.transform, c.transform };
        instance.rotation = Quaternion.identity;
    }
    internal static void Rotate(Quaternion value) { if (instance) instance.rotation = value; }
    private void LateUpdate()
    {
        if (!owner) { Destroy(gameObject); return; }
        var vr = ModXrManager.IsVrEnabled;
        if (vr || wasVr)
            foreach (var reticle in reticles)
                if (reticle) { if (vr) reticle.localRotation = rotation; else reticle.rotation = rotation; }
        wasVr = vr;
    }
    private void OnDestroy() { if (instance == this) instance = null; }
}

[HarmonyPatch]
internal static class DemoReticlePatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeUIController), "Awake")]
    private static void Register(DemoChargeUIController __instance, Image ___m_ReticleOutOfRange, Image ___m_ReticleInRangeUnblocked, Image ___m_ReticleInRangeBlocked) =>
        VrDemoReticle.Register(__instance, ___m_ReticleOutOfRange, ___m_ReticleInRangeUnblocked, ___m_ReticleInRangeBlocked);
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeUIController), "OnRotationChanged")]
    private static void Rotation(DemoChargeRotationChangedEvent ev) => VrDemoReticle.Rotate(ev.NewRotation);
}
