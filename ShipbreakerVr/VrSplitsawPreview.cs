using System.Collections.Generic;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipbreakerVr;

// A two-metre angular guide; native tool data still defines the actual cut/range.
internal sealed class VrSplitsawPreview : MonoBehaviour
{
    private CuttingController tool;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private Material material;
    private MaterialPropertyBlock properties;
    private bool failed;
    internal bool Draw(CuttingController value)
    {
        tool = value;
        if (failed || !tool || !VrAdditionalToolControls.SplitsawActive) { Hide(); return false; }
        try
        {
            if (!material)
            {
                var template = VrAssetManager.LoadBundle("debugrays").LoadAsset<Material>("DebugRayMaterial");
                if (!template || !template.shader || !template.shader.isSupported) return false;
                material = new Material(template); material.SetInt("_CullMode", (int)CullMode.Off);
                properties = new MaterialPropertyBlock();
            }
            var camera = VrAdditionalToolControls.AimCamera();
            var cutLines = tool.ActiveCutData.BuffableCutLines;
            var color = tool.IsCoolingDown ? Color.yellow : tool.AnyValidTargetables ? Color.green : Color.red;
            properties.SetColor("_UnlitColor", color);
            var mask = VrCamera.ViewCamera.cullingMask;
            var layer = 0; while (layer < 31 && (mask & (1 << layer)) == 0) layer++;
            for (var i = 0; i < cutLines.Count; i++)
            {
                if (i == lines.Count)
                {
                    var host = new GameObject("ShipbreakerVr Splitsaw guide"); host.transform.SetParent(transform, false);
                    var line = host.AddComponent<LineRenderer>(); line.sharedMaterial = material;
                    line.positionCount = 2; line.useWorldSpace = true; line.startWidth = line.endWidth = .006f;
                    line.alignment = LineAlignment.View; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                    lines.Add(line);
                }
                var data = cutLines[i];
                CuttingController.GetRotatedCutLine(data, tool.CutRotation, out var offset, out var rotation);
                var center = LynxCameraController.ScreenCenter + offset * LynxCameraController.ScreenWidth;
                var halfSpan = rotation * (data.CutLineWidth * LynxCameraController.ScreenWidth * .5f);
                var a = VrAdditionalToolControls.ScreenRay(camera, center - halfSpan);
                var b = VrAdditionalToolControls.ScreenRay(camera, center + halfSpan);
                var ray = lines[i]; ray.gameObject.layer = layer; ray.enabled = true;
                ray.SetPosition(0, a.GetPoint(2f)); ray.SetPosition(1, b.GetPoint(2f));
                ray.startColor = ray.endColor = color; ray.SetPropertyBlock(properties);
            }
            for (var i = cutLines.Count; i < lines.Count; i++) lines[i].enabled = false;
            return true;
        }
        catch (System.Exception error) { failed = true; Hide(); Debug.LogError("[ShipbreakerVr] Splitsaw guide unavailable: " + error); return false; }
    }
    private void LateUpdate() { if (tool) Draw(tool); }
    private void Hide() { foreach (var line in lines) if (line) line.enabled = false; }
    private void OnDisable() => Hide();
    private void OnDestroy() { if (material) Destroy(material); }
}

[HarmonyPatch(typeof(CuttingUIController), "Update")]
internal static class SplitsawPreviewPatch
{
    private static VrSplitsawPreview preview;
    private static readonly System.Reflection.MethodInfo toggleLines = AccessTools.Method(typeof(CuttingUIController), "ToggleAllCutLines");
    private static void Postfix(CuttingUIController __instance, CuttingController ___m_CuttingController, GameObject ___m_EdgeDetectionPointContainer)
    {
        if (!VrAdditionalToolControls.SplitsawActive) return;
        if (!preview)
        {
            var host = new GameObject("ShipbreakerVr Splitsaw preview");
            // A separate host avoids adding a component to game objects processed by ECS conversion.
            preview = host.AddComponent<VrSplitsawPreview>();
        }
        if (!preview.Draw(___m_CuttingController)) return;
        toggleLines.Invoke(__instance, new object[] { false });
        ___m_EdgeDetectionPointContainer.SetActive(false);
    }
}
