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
		var light = __instance.GetComponent<Light>();
		light.shadows = LightShadows.None;
		__instance.EnableShadows(false);
		__instance.affectsVolumetric = false;
		
		// The fixes above weren't enough, so just disabling all lights for now.
		// This might be fixed if I use OpenXR instead of OpenVR.
		__instance.enabled = false;
		light.enabled = false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LynxCameraController), "Start")]
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
}