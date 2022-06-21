using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[BepInPlugin("ShipbreakerVr", "ShipbreakerVr", "0.2.0")]
public class Plugin : BaseUnityPlugin
{
    private bool lights = true;

    private void Awake()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        gameObject.AddComponent<ModXrManager>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) QualitySettings.SetQualityLevel(0);

        if (Input.GetKeyDown(KeyCode.F6))
        {
            lights = !lights;

            foreach (var light in FindObjectsOfType<Light>())
                light.enabled = lights;
        }
    }
}