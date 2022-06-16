using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Doozy.Engine.UI.Input;
using HarmonyLib;
using InControl;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace ShipbreakerVr
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		// TODO find a good way to check this.
		public static bool IsVrEnabled;
		private void Awake()
		{
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

			// Plugin startup logic
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
			gameObject.AddComponent<XrLoader>();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.F3))
			{
				XRGeneralSettings.Instance.Manager.StartSubsystems();
				IsVrEnabled = true;
			}
			if (Input.GetKeyDown(KeyCode.F4))
			{
				XRGeneralSettings.Instance.Manager.StopSubsystems();
				IsVrEnabled = false;
			}
		}
	}
}