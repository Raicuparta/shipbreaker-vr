using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[HarmonyPatch]
public static class Patches
{
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