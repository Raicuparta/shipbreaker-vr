using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BBI;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[HarmonyPatch]
internal static class MovementPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ThrustController), "Update");
        yield return AccessTools.Method(typeof(OrientationController), "HandleAxisRotation");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = new List<CodeInstruction>(instructions);
        var count = 0;
        foreach (var instruction in code)
        {
            if (!(instruction.operand is MethodInfo method)) continue;
            string replacement = null;
            if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetOneAxisInputControlValue")) replacement = "OneAxis";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetTwoAxisInputControlVector")) replacement = "TwoAxis";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputIsPressed")) replacement = "Pressed";
            else if (method == AccessTools.PropertyGetter(typeof(LynxControls), "LastInputType")) replacement = "InputType";
            if (replacement == null) continue;
            instruction.opcode = OpCodes.Call;
            instruction.operand = AccessTools.Method(typeof(VrMovementControls), replacement);
            count++;
        }
        var expected = original.DeclaringType == typeof(ThrustController) ? 15 : 14;
        if (count != expected) throw new InvalidOperationException($"Movement hook changed: {original.Name}, expected {expected}, found {count}.");
        Debug.Log($"[ShipbreakerVr] Movement hook verified: {original.DeclaringType.Name}.{original.Name}; calls={count}");
        return code;
    }
}
