using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace ShipbreakerVr;

[HarmonyPatch]
internal static class GrapplePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(RaycastSystem), "OnUpdate");
        foreach (var method in new[] { "UpdateGrapplingHook", "CheckIfObjectInBounds", "OnThrowPressed", "OnRangedThrowPressed" })
            yield return AccessTools.Method(typeof(GrapplingHook), method);
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var output = new List<CodeInstruction>();
        var count = 0;
        foreach (var instruction in instructions)
        {
            var method = instruction.operand as MethodInfo;
            string replacement = null;
            if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCamera"))
            {
                replacement = original.DeclaringType == typeof(RaycastSystem) ? "RaycastCamera" : "AimCamera";
                if (replacement == "RaycastCamera")
                {
                    var load = new CodeInstruction(OpCodes.Ldarg_0);
                    load.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    load.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    output.Add(load);
                }
            }
            else if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCameraTransform")) replacement = "AimTransform";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputWasPressed")) replacement = "WasPressed";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputIsPressed")) replacement = "IsPressed";
            else if (method == AccessTools.Method(typeof(LynxControlExtensions), "GetInputWasReleased")) replacement = "WasReleased";
            if (replacement != null)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(VrGrappleControls), replacement);
                count++;
            }
            output.Add(instruction);
        }
        var expected = original.DeclaringType == typeof(RaycastSystem) ? 4 : original.Name == "UpdateGrapplingHook" ? 14 : original.Name == "CheckIfObjectInBounds" ? 1 : 3;
        if (count != expected) throw new InvalidOperationException($"Grapple hook changed: {original.Name}, expected {expected}, found {count}.");
        Debug.Log($"[ShipbreakerVr] Grapple hook verified: {original.DeclaringType.Name}.{original.Name}; calls={count}");
        return output;
    }
}

[HarmonyPatch]
internal static class GrappleLifecyclePatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(GrapplingHook), "Start")]
    private static void Register(GrapplingHook __instance, EquipmentController ___m_EquipmentController, LaserRope ___m_GrapplingRope) =>
        VrGrappleControls.Register(__instance, ___m_EquipmentController, ___m_GrapplingRope);

    [HarmonyPrefix, HarmonyPatch(typeof(GrapplingHook), "OnRangedThrowPressed")]
    private static bool GuardPendingPush() => VrGrappleControls.AllowPush();

    [HarmonyPostfix, HarmonyPatch(typeof(LaserRope), "get_StartPoint")]
    private static void BeamOrigin(LaserRope __instance, ref Vector3 __result)
    {
        if (VrGrappleControls.Owns(__instance))
        {
            var aim = VrGrappleControls.AimTransform();
            __result = VrAvatarVisuals.ToolBeamOrigin(EquipmentController.Equipment.GrappleHook, new Pose(aim.position, aim.rotation));
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(LaserRope), "GetPositionForceInfo")]
    private static void HandSteering(LaserRope __instance, ref LaserRope.AnchorData connectedAnchor)
    {
        // Change only this call's copy of the player manipulator direction. The
        // physical player anchor, spring/damping, masses and force caps stay native.
        if (connectedAnchor.IsManipulator && connectedAnchor.Anchor && VrGrappleControls.Owns(__instance))
            connectedAnchor.LocalRopeDirection = connectedAnchor.Anchor.transform.InverseTransformDirection(VrGrappleControls.AimTransform().forward);
    }
}
