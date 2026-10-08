using BBI.Unity.Game;
using BepInEx.Configuration;
using HarmonyLib;
using ShipbreakerVr.Tracking;
using UnityEngine;

namespace ShipbreakerVr;

// Read live buffable range data; visual hints never change targeting or tool rules.
internal static class VrToolPresentation
{
    internal static ConfigEntry<bool> DebugRays, ToolGuides;
    internal static EquipmentController Equipment;
    internal static CuttingToolController Cutter;
    internal static ScalpelController Stinger;
    internal static CuttingController Splitsaw;
    internal static GrapplingHook Grapple;
    internal static TetherController Tether;
    internal static ITethersData TetherData;
    internal static DemoChargeController Demo;
    internal static IDemoChargeControllerData DemoData;
    internal static DemoChargeState DemoState;
    internal static Rigidbody PlayerBody;
    internal static void Configure(ConfigFile config)
    {
        DebugRays = config.Bind("Presentation", "ShowDebugRays", false, "Show both diagnostic controller rays instead of contextual tool guides during gameplay. Menus never display decorative rays.");
        ToolGuides = config.Bind("Presentation", "ToolRangeGuides", true, "Right-hand beam ends at the first surface or live upgraded tool range. The ring marks the end; it does not guarantee a valid/cuttable target.");
    }
    internal static bool GameplayRays =>
        GameSession.CurrentGameState == GameSession.GameState.Gameplay && !EquipmentController.ToolMenuOpen;
    internal static bool TryRange(out float range, out int mask, out QueryTriggerInteraction triggers, out string mode)
    {
        range = 0; mask = Physics.DefaultRaycastLayers; mode = ""; triggers = QueryTriggerInteraction.UseGlobal;
        if (!Equipment || GameSession.CurrentGameState != GameSession.GameState.Gameplay || EquipmentController.ToolMenuOpen ||
            !LynxControls.Instance || !LynxControls.Instance.IsGameFocused || VrMenuControls.PauseInputConsumed ||
            LynxControls.Instance.TryGetLoadedActionSet(LynxControls.PlayerActionSetTypes.GameplayActions)?.Enabled != true) return false;
        switch (Equipment.CurrentEquipment)
        {
            case EquipmentController.Equipment.CuttingTool:
                if (!Cutter) return false;
                if (Cutter.CurrentMode == CuttingToolController.CutterMode.Scalpel && Stinger && Stinger.Data != null)
                { range = Stinger.Data.Range; mask = Stinger.Data.LayerMask; mode = "Stinger"; }
                else if (Cutter.CurrentMode == CuttingToolController.CutterMode.Cutter && Splitsaw && Splitsaw.ActiveCutData != null)
                { range = Splitsaw.ActiveCutData.Range; mask = Splitsaw.RaycastLayerMask; mode = "Splitsaw center guide"; }
                break;
            case EquipmentController.Equipment.GrappleHook:
                if (Tether && Tether.State == TetherController.TetherState.Placing && TetherData != null)
                { range = TetherData.LaunchRange; mask = TetherData.RaycastLayerMask; mode = "Tether targeting reach"; }
                else if (Grapple && Main.Instance)
                { range = Grapple.MaxDistance; mask = Main.Instance.MainSettings.RaycastSettings.BaseLayerMask; mode = "Grapple"; }
                break;
            case EquipmentController.Equipment.DemoCharge:
                if (Demo && DemoData != null && (DemoState == DemoChargeState.Placement || DemoState == DemoChargeState.Placing || DemoState == DemoChargeState.Throwing))
                { range = DemoData.RaycastDistance; mask = DemoData.RaycastLayerMask; triggers = QueryTriggerInteraction.Ignore; mode = "Demo placement reach (not throw distance)"; }
                break;
        }
        return PresentationGeometry.ValidRange(range);
    }
}

[HarmonyPatch]
internal static class ToolPresentationPatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(EquipmentController), "Start")]
    private static void Equipment(EquipmentController __instance) => VrToolPresentation.Equipment = __instance;
    [HarmonyPostfix, HarmonyPatch(typeof(CuttingToolController), "Awake")]
    private static void Cutter(CuttingToolController __instance) => VrToolPresentation.Cutter = __instance;
    [HarmonyPostfix, HarmonyPatch(typeof(ScalpelController), "Awake")]
    private static void Stinger(ScalpelController __instance) => VrToolPresentation.Stinger = __instance;
    [HarmonyPostfix, HarmonyPatch(typeof(CuttingController), "Awake")]
    private static void Splitsaw(CuttingController __instance) => VrToolPresentation.Splitsaw = __instance;
    [HarmonyPostfix, HarmonyPatch(typeof(GrapplingHook), "Awake")]
    private static void Grapple(GrapplingHook __instance, Rigidbody ___m_PlayerRigidbody)
    { VrToolPresentation.Grapple = __instance; VrToolPresentation.PlayerBody = ___m_PlayerRigidbody; }
    [HarmonyPostfix, HarmonyPatch(typeof(TetherController), "Awake")]
    private static void Tether(TetherController __instance, ITethersData ___mData)
    { VrToolPresentation.Tether = __instance; VrToolPresentation.TetherData = ___mData; }
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeController), "Awake")]
    private static void Demo(DemoChargeController __instance, IDemoChargeControllerData ___mData, GameObject ___m_DetonatorMeshRoot, Animator ___m_HandAnimatorController)
    { VrToolPresentation.Demo = __instance; VrToolPresentation.DemoData = ___mData; VrAvatarVisuals.RegisterDetonator(___m_DetonatorMeshRoot, ___m_HandAnimatorController); }
    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeController), "SetState")]
    private static void DemoState(DemoChargeState newState) => VrToolPresentation.DemoState = newState;
}
