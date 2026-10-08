using BBI.Unity.Game;
using ShipbreakerVr.Tracking;
using UnityEngine;

namespace ShipbreakerVr;

// Game-specific Stinger adapter. No global camera or input state is replaced.
internal sealed class VrStingerControls : MonoBehaviour
{
    private static VrStingerControls instance;
    private CuttingToolController tool;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly TriggerLatch trigger = new TriggerLatch();
    private InputSystemUpdateBridge bridge;
    private Transform aim;
    private int sampledFrame = -1;
    private bool valid;
    private string lastStatus;

    private bool ControlsStinger => ShipbreakerVrMod.StingerMotionControls.Value && VrInputMode.MotionActive &&
        tool && tool.CurrentMode == CuttingToolController.CutterMode.Scalpel && tool.EquipementController &&
        tool.EquipementController.CurrentEquipment == EquipmentController.Equipment.CuttingTool;

    private void Awake()
    {
        instance = this;
        aim = new GameObject("ShipbreakerVr Stinger aim").transform;
        aim.SetParent(transform, false);
    }

    // Called before the game's cutter update; each frame uses one consistent action/pose sample.
    internal static void BeforeToolUpdate(CuttingToolController controller)
    {
        if (!instance) return;
        if (instance.tool != controller)
        {
            instance.tool = controller;
            instance.trigger.Sample(false, 0f);
            instance.sampledFrame = -1;
        }
        instance.Sample();
    }

    private void Update()
    {
        // Reset even while no equipment update is running (pause, disable or scene teardown).
        if (!ControlsStinger || EquipmentController.ToolMenuOpen || GameSession.CurrentGameState != GameSession.GameState.Gameplay || !Application.isFocused)
        {
            valid = false;
            trigger.Sample(false, 0f);
        }
    }

    private void Sample()
    {
        if (!ControlsStinger) { valid = false; trigger.Sample(false, 0f); return; }
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        valid = false;
        var fire = LynxControls.Instance ? LynxControls.Instance.TryGetLoadedPlayerAction(
            LynxControls.ActionSetAndId.FromEnum(GameplayActions.GameplayActionSet.CutterFire)) : null;
        var eligible = ControlsStinger && GameSession.CurrentGameState == GameSession.GameState.Gameplay &&
            Application.isFocused && VrCamera.BodyTransform && VrCamera.ViewCamera && VrCamera.ViewCamera.isActiveAndEnabled &&
            !EquipmentController.ToolMenuOpen && !VrMenuControls.PauseInputConsumed && !tool.IsCutterFireActionBlocked && fire != null && fire.Enabled;
        if (eligible)
        {
            if (bridge == null) bridge = new InputSystemUpdateBridge();
            bridge.UpdateIfNeeded();
            tracking.Sample();
            valid = tracking.Head.IsValid && tracking.RightHand.Aim.IsValid && tracking.RightHand.Grip.IsValid;
            if (valid)
            {
                var body = VrCamera.BodyTransform;
                var world = ShipbreakerTrackingSpace.ToWorld(tracking.Head, tracking.RightHand.Aim,
                    body.position, body.rotation, VrCamera.EyeOffset);
                aim.SetPositionAndRotation(world.position, world.rotation);
            }
        }
        else
        {
            tracking.Clear();
            bridge?.Dispose();
            bridge = null;
        }
        trigger.Sample(eligible && valid, tracking.RightHand.Trigger);
        var status = !ControlsStinger ? "inactive" : !eligible ? "blocked by game state/focus/action" :
            !valid ? "tracking unavailable" : "right-hand aim ready; release then squeeze trigger";
        if (status != lastStatus)
        {
            lastStatus = status;
            Debug.Log($"[ShipbreakerVr] Stinger controls: {status}");
        }
        if (trigger.Pressed) Debug.Log("[ShipbreakerVr] Stinger right-trigger press");
    }

    internal static Transform AimTransform()
    {
        if (instance) instance.Sample();
        return instance && instance.ControlsStinger && instance.valid ? instance.aim : LynxCameraController.MainCameraTransform;
    }

    internal static bool HasTrackedAim()
    {
        if (instance) instance.Sample();
        return instance && instance.ControlsStinger && instance.valid;
    }

    internal static Vector3 BeamOrigin(Transform original, Vector3 offset)
    {
        if (instance) instance.Sample();
        return instance && instance.ControlsStinger && instance.valid ? VrAvatarVisuals.ToolBeamOrigin(EquipmentController.Equipment.CuttingTool, new Pose(instance.aim.position, instance.aim.rotation)) : original.TransformPoint(offset);
    }

    internal static bool WasPressed(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (instance) instance.Sample();
        return instance && instance.ControlsStinger && action.Equals(LynxControls.ActionSetAndId.FromEnum(GameplayActions.GameplayActionSet.CutterFire))
            ? instance.trigger.Pressed : controls.GetInputWasPressed(action);
    }

    internal static bool IsPressed(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (instance) instance.Sample();
        return instance && instance.ControlsStinger && action.Equals(LynxControls.ActionSetAndId.FromEnum(GameplayActions.GameplayActionSet.CutterFire))
            ? instance.trigger.Held : controls.GetInputIsPressed(action);
    }

    private void OnDisable()
    {
        valid = false;
        trigger.Sample(false, 0f);
        bridge?.Dispose();
        bridge = null;
        if (tool && tool.CurrentMode == CuttingToolController.CutterMode.Scalpel && tool.State == CuttingState.Cutting)
            tool.SetState(CuttingState.Ready);
    }

    internal static void InputOwnerChanged()
    {
        if (!instance) return;
        instance.OnDisable();
        instance.sampledFrame = -1;
    }

    private void OnDestroy() { if (instance == this) instance = null; }
}
