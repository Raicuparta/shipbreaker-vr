using System;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;

namespace ShipbreakerVr;

public static class VrAssetManager
{
    private const string AssetsDir = "ShipbreakerVr/AssetBundles";
    private static readonly Dictionary<string, AssetBundle> Bundles = new Dictionary<string, AssetBundle>();

    public static AssetBundle LoadBundle(string assetName)
    {
        if (Bundles.TryGetValue(assetName, out var cached) && cached) return cached;
        Debug.Log($"loading bundle {assetName} in {Paths.PluginPath}...");
        var bundle = AssetBundle.LoadFromFile(Path.Combine(Paths.PluginPath, Path.Combine(AssetsDir, assetName)));

        if (bundle == null) throw new Exception("Failed to load asset bundle " + assetName);

        Debug.Log($"Loaded bundle {bundle.name}");
        Bundles[assetName] = bundle;

        return bundle;
    }
}
