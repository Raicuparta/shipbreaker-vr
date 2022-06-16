using System;
using System.Reflection;
using BepInEx;
using Doozy.Engine.UI.Input;
using HarmonyLib;

namespace ShipbreakerVr
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		private void Awake()
		{
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

			// Plugin startup logic
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
			gameObject.AddComponent<XrLoader>();
		}
	}
}