using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MilestoneBuild
{
    [MenuItem("Shipbreaker VR/Build dependencies and debug material")]
    public static void BuildFromEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
        {
            EditorUtility.DisplayDialog("Shipbreaker VR", "Stop Play mode and wait for any current build to finish first.", "OK");
            return;
        }
        try
        {
            Debug.Log("Shipbreaker VR: starting dependency and debug-ray material build from the editor.");
            Build();
            Debug.Log("Shipbreaker VR: Unity dependency and debug-ray material build succeeded.");
            EditorUtility.DisplayDialog("Shipbreaker VR: Unity build succeeded",
                "Close Unity, then run scripts/build.ps1 with -GameDir and -SkipUnity to compile the mod. No game files have been changed.", "OK");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Shipbreaker VR: Unity build failed",
                "The build stopped. Review the Console and editor log for the error details.", "OK");
        }
    }

    public static void Build()
    {
        if (Application.unityVersion != "2020.3.17f1")
            throw new InvalidOperationException("Use Unity 2020.3.17f1; do not upgrade the game's XR runtime independently.");
        Directory.CreateDirectory("Build");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scene.unity" },
            locationPathName = "Build/ShipbreakerVrUnity.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (result.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Unity dependency player failed to build: " + result.summary.result);
        Directory.CreateDirectory("AssetBundles");
        // Use HDRP's own stereo-capable unlit shader, not a Built-in Pipeline shader.
        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            AssetDatabase.CreateFolder("Assets", "Generated");
        const string materialPath = "Assets/Generated/DebugRay.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find("HDRP/Unlit");
        if (shader == null) throw new InvalidOperationException("HDRP/Unlit shader missing.");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetColor("_UnlitColor", Color.white);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        // Keep the known-good xrmanager bundle unchanged. Build only the new material bundle.
        var manifest = BuildPipeline.BuildAssetBundles("AssetBundles", new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "debugrays",
                assetNames = new[] { materialPath },
                addressableNames = new[] { "DebugRayMaterial" }
            }
        }, BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new InvalidOperationException("Debug ray asset bundle build failed.");
    }
}
