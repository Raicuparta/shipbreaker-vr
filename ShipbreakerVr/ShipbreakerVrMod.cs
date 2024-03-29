﻿using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[BepInPlugin("ShipbreakerVr", "ShipbreakerVr", "0.3.0")]
public class ShipbreakerVrMod : BaseUnityPlugin
{
    private bool enableLights = true;
    private List<Light> lights;

    private void Awake()
    {
        Debug.Log($"Loaded ShipbreakerVr. Game version: {Application.version}");

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        gameObject.AddComponent<ModXrManager>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) SetPotatoQuality();
        if (Input.GetKeyDown(KeyCode.F6)) DisableLights();
    }

    private static void SetPotatoQuality()
    {
        QualitySettings.SetQualityLevel(0);
    }

    private void DisableLights()
    {
        enableLights = !enableLights;

        if (!enableLights)
        {
            lights = new List<Light>();
            foreach (var light in FindObjectsOfType<Light>())
            {
                if (light.enabled) lights.Add(light);
                light.enabled = false;
            }
        }
        else
        {
            foreach (var light in lights) light.enabled = true;
        }
    }
}