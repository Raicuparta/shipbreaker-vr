using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private bool lights = true;

    // TODO find a good way to check this.
    private void Awake()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        // Plugin startup logic
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        gameObject.AddComponent<ModXrManager>();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.F5))
        {
            if (Input.GetKeyDown(KeyCode.Alpha0)) QualitySettings.SetQualityLevel(0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) QualitySettings.SetQualityLevel(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) QualitySettings.SetQualityLevel(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) QualitySettings.SetQualityLevel(3);
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            lights = !lights;

            foreach (var light in FindObjectsOfType<Light>())
                light.enabled = lights;
        }
    }
}