using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ShipbreakerVr;

[HarmonyPatch]
public static class Patches
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(HDAdditionalLightData), "Awake")]
	private static void DisableShadows(HDAdditionalLightData __instance)
	{
		Debug.Log($"#### FOund light {__instance.name}");
		var light = __instance.GetComponent<Light>();
		light.shadows = LightShadows.None;
		__instance.EnableShadows(false);
		__instance.affectsVolumetric = false;
		__instance.enabled = false;
		light.enabled = false;
	}
}