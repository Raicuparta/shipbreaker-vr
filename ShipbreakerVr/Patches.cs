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
		__instance.GetComponent<Light>().shadows = LightShadows.None;
	}
}