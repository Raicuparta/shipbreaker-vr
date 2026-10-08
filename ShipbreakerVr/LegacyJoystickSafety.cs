using System;
using System.Collections.Generic;
using HarmonyLib;
using InControl;

namespace ShipbreakerVr;

// The shipped manager enumerates every Unity joystick, but UnityInputDevice's
// query tables only have MaxDevices rows. An exception mid-update leaves other
// devices with uncommitted ticks and prevents every subsequent input update.
[HarmonyPatch]
internal static class LegacyJoystickSafety
{
    private static readonly HashSet<string> reported = new HashSet<string>();
    internal static bool Install()
    {
        var harmony = new Harmony("ShipbreakerVr.LegacyJoystickSafety");
        try
        {
            harmony.CreateClassProcessor(typeof(LegacyJoystickSafety)).Patch();
            return true;
        }
        catch (Exception error)
        {
            try { harmony.UnpatchSelf(); }
            catch (Exception cleanupError) { Logger.LogWarning("[ShipbreakerVr] Legacy joystick hook cleanup failed: " + cleanupError.Message); }
            Logger.LogWarning("[ShipbreakerVr] Legacy joystick bounds protection unavailable: " + error);
            return false;
        }
    }
    private static bool ValidSlot(int id) => id >= 1 && id <= UnityInputDevice.MaxDevices;
    private static void Report(int id, string kind, int index)
    {
        var key = id + "/" + kind + "/" + index;
        if (reported.Add(key)) Logger.LogWarning($"[ShipbreakerVr] Ignored unsupported legacy joystick query: slot={id}, kind={kind}, index={index}; supported slots=1..{UnityInputDevice.MaxDevices}. OpenXR controls remain independent.");
    }

    [HarmonyPrefix, HarmonyPatch(typeof(UnityInputDeviceManager), "DetectJoystickDevice")]
    internal static bool Detect(int unityJoystickId)
    {
        if (ValidSlot(unityJoystickId)) return true;
        Report(unityJoystickId, "device", -1);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(UnityInputDevice), nameof(UnityInputDevice.ReadRawButtonState))]
    internal static bool Button(UnityInputDevice __instance, int index, ref bool __result)
    {
        if (ValidSlot(__instance.JoystickId) && index >= 0 && index < UnityInputDevice.MaxButtons) return true;
        __result = false;
        if (!ValidSlot(__instance.JoystickId) || index < 0) Report(__instance.JoystickId, "button", index);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(UnityInputDevice), nameof(UnityInputDevice.ReadRawAnalogValue))]
    internal static bool Analog(UnityInputDevice __instance, int index, ref float __result)
    {
        if (ValidSlot(__instance.JoystickId) && index >= 0 && index < UnityInputDevice.MaxAnalogs) return true;
        __result = 0f;
        if (!ValidSlot(__instance.JoystickId) || index < 0) Report(__instance.JoystickId, "axis", index);
        return false;
    }
}
