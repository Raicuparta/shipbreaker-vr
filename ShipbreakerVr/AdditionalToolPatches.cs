using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[HarmonyPatch]
internal static class AdditionalToolPatches
{
    // Explicit scope/counts: original native behavior outside these methods is preserved.
    private static readonly Dictionary<MethodBase, int> targets = new Dictionary<MethodBase, int>();
    private static IEnumerable<MethodBase> TargetMethods()
    {
        targets.Clear();
        void Add(Type type, string name, int count) => targets.Add(AccessTools.Method(type, name), count);
        Add(typeof(CuttingController), "UpdateCutter", 1);
        Add(typeof(CuttingController), "HandleCuttingReady", 1);
        Add(typeof(CuttingController), "HandleCutterDisabled", 1);
        Add(typeof(CuttingController), "TryPerformCut", 4);
        Add(typeof(CuttingController), "TryGetCutLineTargetables", 7);
        Add(typeof(CuttingController), "TryGetTargetableInRange", 3);
        Add(typeof(CuttingFXController), "Update", 1);
        Add(typeof(CuttingFXController), "StartCutFX", 4);
        Add(typeof(CuttingFXController), "StartCutLine", 10);
        Add(typeof(CuttingFXController), "UpdateCutLine", 1);
        Add(AccessTools.Inner(typeof(CuttingFXController), "CutLineState"), "UpdateLine", 2);
        Add(typeof(DemoChargeController), "UpdatePlacementInputs", 2);
        Add(typeof(DemoChargeController), "UpdateDetonationState", 1);
        Add(typeof(DemoChargeController), "UpdateDetonatingState", 1);
        Add(typeof(DemoChargeController), "UpdateDetonationFailedState", 1);
        Add(typeof(Scanner), "HandleScannerInput", 4);
        Add(typeof(Scanner), "HandleRaycasting", 2);
        return targets.Keys;
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = new List<CodeInstruction>(instructions);
        var count = 0;
        foreach (var instruction in code)
        {
            if (!(instruction.operand is MethodInfo method)) continue;
            string replacement = null;
            if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCamera"))
                replacement = original.DeclaringType == typeof(Scanner) ? "ScannerCamera" : "AimCamera";
            else if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCameraTransform")) replacement = "AimTransform";
            else if (method == AccessTools.Method(typeof(Camera), "ScreenPointToRay", new[] { typeof(Vector3) })) replacement = "ScreenRay";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputWasPressed")) replacement = "WasPressed";
            else if (original.DeclaringType == typeof(CuttingFXController) &&
                method == AccessTools.Method(typeof(Transform), "TransformPoint", new[] { typeof(Vector3) })) replacement = "BeamOrigin";
            else if (original.DeclaringType == typeof(CuttingFXController) && original.Name == "StartCutLine" &&
                method == AccessTools.PropertyGetter(typeof(Transform), "position")) replacement = "BeamPosition";
            if (replacement == null) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(VrAdditionalToolControls), replacement);
            count++;
        }
        if (count != targets[original]) throw new InvalidOperationException($"Additional tool hook changed: {original.Name}: expected {targets[original]}, found {count}.");
        Debug.Log($"[ShipbreakerVr] Additional tool hook verified: {original.DeclaringType.Name}.{original.Name}; calls={count}");
        return code;
    }
}

[HarmonyPatch]
internal static class AdditionalToolLifecyclePatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(EquipmentController), "Start")]
    private static void RegisterEquipment(EquipmentController __instance) => VrAdditionalToolControls.RegisterEquipment(__instance);
    [HarmonyPrefix, HarmonyPatch(typeof(CuttingToolController), "Update")]
    private static void RegisterCutter(CuttingToolController __instance) => VrAdditionalToolControls.RegisterCutter(__instance);
    [HarmonyPrefix, HarmonyPatch(typeof(CuttingController), "TryPerformCut")]
    private static bool GuardCut() => VrAdditionalToolControls.AllowCut();
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeController), "get_PlayerTransform")]
    private static void DemoAim(ref Transform __result) => VrAdditionalToolControls.DemoTransform(ref __result);
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeController), "get_ThrowOffset")]
    private static void DemoThrow(ref Transform __result) => VrAdditionalToolControls.DemoTransform(ref __result);
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeController), "SetState")]
    private static void DemoMode(DemoChargeState newState) => VrAdditionalToolControls.DemoStateChanged(newState);
}

[HarmonyPatch]
internal static class TetherPatches
{
    private static readonly Dictionary<string, int> counts = new Dictionary<string, int> {
        {"HandleReady", 1}, {"HandlePlacing", 4}, {"TryDespawnTether", 1},
        {"TryGetTetherPoint", 4}, {"HandlePreviewLine", 2}, {"SpawnFireFX", 3}, {"SpawnHookFX", 1}
    };
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in counts.Keys) yield return AccessTools.Method(typeof(TetherController), name);
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = new List<CodeInstruction>();
        var count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, AccessTools.Field(typeof(TetherController), "m_GunBarrel")))
            {
                code.Add(instruction);
                code.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VrGrappleControls), "TetherBarrel")));
                count++; continue;
            }
            var method = instruction.operand as MethodInfo;
            string replacement = null;
            if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCamera")) replacement = "AimCamera";
            else if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCameraTransform")) replacement = "AimTransform";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputWasPressed")) replacement = "TetherWasPressed";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputWasReleased")) replacement = "TetherWasReleased";
            if (replacement != null)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(VrGrappleControls), replacement);
                count++;
            }
            code.Add(instruction);
        }
        if (count != counts[original.Name]) throw new InvalidOperationException($"Tether hook changed: {original.Name}: expected {counts[original.Name]}, found {count}.");
        Debug.Log($"[ShipbreakerVr] Tether hook verified: {original.Name}; calls={count}");
        return code;
    }
}

[HarmonyPatch(typeof(TetherController), "Update")]
internal static class TetherUpdatePatch
{
    [HarmonyPrefix]
    private static void BeforeUpdate(TetherController __instance) => VrGrappleControls.BeforeTetherUpdate(__instance);
}
