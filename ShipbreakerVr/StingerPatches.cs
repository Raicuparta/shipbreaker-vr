using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[HarmonyPatch]
internal static class StingerPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ScalpelController), "UpdateTargeting");
        yield return AccessTools.Method(typeof(ScalpelTarget), "Reset");
        yield return AccessTools.Method(typeof(ScalpelController), "HandleReadyState");
        yield return AccessTools.Method(typeof(ScalpelController), "HandleCuttingState");
        yield return AccessTools.Method(typeof(ScalpelController), "HandleDisabledState");
        yield return AccessTools.Method(typeof(ScalpelFXController), "UpdateLine");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = new List<CodeInstruction>(instructions);
        var replaced = 0;
        foreach (var instruction in code)
        {
            if (!(instruction.operand is MethodInfo method)) continue;
            string replacement = null;
            if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCameraTransform")) replacement = "AimTransform";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputWasPressed")) replacement = "WasPressed";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputIsPressed")) replacement = "IsPressed";
            else if (original.DeclaringType == typeof(ScalpelFXController) &&
                     method == AccessTools.Method(typeof(Transform), "TransformPoint", new[] { typeof(Vector3) })) replacement = "BeamOrigin";
            if (replacement == null) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(VrStingerControls), replacement);
            replaced++;
        }
        var expected = original.Name == "UpdateTargeting" ? 2 : original.Name == "Reset" ? 3 : 1;
        if (replaced != expected) throw new InvalidOperationException($"Stinger hook changed: {original.Name}, expected {expected} call sites, found {replaced}.");
        Debug.Log($"[ShipbreakerVr] Stinger hook verified: {original.DeclaringType.Name}.{original.Name}; calls={replaced}");
        return code;
    }
}

[HarmonyPatch(typeof(CuttingToolController), "Update")]
internal static class StingerUpdatePatch
{
    private static void Prefix(CuttingToolController __instance) => VrStingerControls.BeforeToolUpdate(__instance);
}

[HarmonyPatch(typeof(ScalpelFXController), "SetLine")]
internal static class StingerBeamStartPatch
{
    private static readonly MethodInfo updateLine = AccessTools.Method(typeof(ScalpelFXController), "UpdateLine");

    private static void Postfix(ScalpelFXController __instance, bool ___mIsInitialized)
    {
        // SetLine enables two vertices and the burning collider, but leaves their
        // previous positions until FX.Update. Refresh both before this firing event
        // returns, using the target already calculated by UpdateScalpel.
        if (___mIsInitialized && VrStingerControls.HasTrackedAim()) updateLine.Invoke(__instance, null);
    }
}
