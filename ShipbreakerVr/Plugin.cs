using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR.Management;

namespace ShipbreakerVr
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		// TODO find a good way to check this.
		private void Awake()
		{
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

			// Plugin startup logic
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
			gameObject.AddComponent<ModXrManager>();
		}
	}
}