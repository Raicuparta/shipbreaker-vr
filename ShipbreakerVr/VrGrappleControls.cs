using BBI.Unity.Game;
using BepInEx.Configuration;
using ShipbreakerVr.Tracking;
using UnityEngine;
using Action = BBI.Unity.Game.GameplayActions.GameplayActionSet;

namespace ShipbreakerVr;

internal sealed class VrGrappleControls : MonoBehaviour
{
    private static VrGrappleControls instance;
    private static ConfigEntry<bool> enabledSetting;
    private GrapplingHook hook;
    private EquipmentController equipment;
    private LaserRope rope;
    private TetherController tethers;
    private readonly TriggerLatch tether = new TriggerLatch();
    private bool tetherReleased, cancelTetherGrab;
    private Camera aimCamera;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly TriggerLatch grab = new TriggerLatch(), retract = new TriggerLatch();
    private InputSystemUpdateBridge bridge;
    private bool valid, retractReleased, owned, failed;
    private int sampledFrame = -1;
    private string lastStatus;

    internal static void Configure(ConfigFile config) => enabledSetting = config.Bind("Controllers", "GrappleMotionControls", true,
        "Right-hand grapple aim and hold-trigger grab; left trigger retracts an attached object. X remains native push. Empty grapple: left trigger holds a tether preview, release at its destination; D-pad down recalls.");
    private bool Controls => enabledSetting.Value && VrInputMode.MotionActive && hook && equipment && equipment.CurrentEquipment == EquipmentController.Equipment.GrappleHook;
    private bool Context => Controls && !failed && GameSession.CurrentGameState == GameSession.GameState.Gameplay &&
        Application.isFocused && LynxControls.Instance && LynxControls.Instance.IsGameFocused &&
        !EquipmentController.ToolMenuOpen && !VrMenuControls.PauseInputConsumed &&
        LynxControls.Instance.TryGetLoadedActionSet(LynxControls.PlayerActionSetTypes.GameplayActions)?.Enabled == true && VrCamera.BodyTransform && VrCamera.ViewCamera;

    private void Awake()
    {
        instance = this;
        var host = new GameObject("ShipbreakerVr grapple projection");
        host.transform.SetParent(transform, false);
        aimCamera = host.AddComponent<Camera>();
        aimCamera.enabled = false;
        aimCamera.stereoTargetEye = StereoTargetEyeMask.None;
    }

    internal static void Register(GrapplingHook value, EquipmentController equipment, LaserRope rope)
    {
        if (!instance) return;
        instance.StopOwnedGrapple();
        instance.hook = value; instance.equipment = equipment; instance.rope = rope;
        instance.sampledFrame = -1;
    }

    private static bool Allowed(Action action) => LynxControls.Instance && LynxControls.Instance.TryGetLoadedPlayerAction(LynxControls.ActionSetAndId.FromEnum(action))?.Enabled == true;

    private void Update()
    {
        if (!Context) StopOwnedGrapple();
    }

    private void StopOwnedGrapple()
    {
        valid = false;
        grab.Sample(false, 0f); retract.Sample(false, 0f); retractReleased = false;
        tether.Sample(false, 0f); tetherReleased = cancelTetherGrab = false;
        var wasOwned = owned;
        owned = false; // Release callbacks may read the rope origin and re-enter this adapter.
        if (wasOwned && tethers && tethers.State == TetherController.TetherState.Placing) tethers.EquipmentChanged();
        if (wasOwned && hook)
        {
            if (hook.PushChargeTime > 0f) hook.CancelPushCharge();
            if (hook.GrappledRigidbody) hook.OnGrappleReleased();
        }
        bridge?.Dispose(); bridge = null;
        tracking.Clear();
    }

    private void Sample()
    {
        // Context can change more than once within a rendered frame.
        if (!Context) { StopOwnedGrapple(); return; }
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        try
        {
            if (bridge == null) bridge = new InputSystemUpdateBridge();
            bridge.UpdateIfNeeded(); tracking.Sample();
            valid = tracking.Head.IsValid && tracking.RightHand.Aim.IsValid && tracking.RightHand.Grip.IsValid;
            if (!valid) { StopOwnedGrapple(); return; }
            owned = true;
            var body = VrCamera.BodyTransform;
            var pose = ShipbreakerTrackingSpace.ToWorld(tracking.Head, tracking.RightHand.Aim, body.position, body.rotation, VrCamera.EyeOffset);
            var source = LynxCameraController.MainCamera;
            if (source) aimCamera.CopyFrom(source);
            aimCamera.enabled = false; aimCamera.stereoTargetEye = StereoTargetEyeMask.None;
            aimCamera.targetTexture = null;
            aimCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            aimCamera.ResetWorldToCameraMatrix();
            var wasGrabbed = grab.Held;
            var wasRetracting = retract.Held;
            grab.Sample(Allowed(Action.GrappleFire), tracking.RightHand.Trigger);
            retract.Sample(Allowed(Action.RetractionModifier) && tracking.LeftHand.Grip.IsValid && hook.GrappledRigidbody, tracking.LeftHand.Trigger);
            retractReleased = wasRetracting && !retract.Held;
            cancelTetherGrab = tethers && tethers.State == TetherController.TetherState.Placing && grab.Pressed;
            var allowTether = Allowed(Action.PlaceTether) && tracking.LeftHand.Grip.IsValid && !hook.GrappledRigidbody && !grab.Held;
            tether.Sample(allowTether, tracking.LeftHand.Trigger);
            tetherReleased = tether.Released;
            if (wasGrabbed && !grab.Held && hook.GrappledRigidbody) hook.OnGrappleReleased();
            if (lastStatus != "ready") { lastStatus = "ready"; Debug.Log("[ShipbreakerVr] Grapple hand aim ready; right trigger holds, left trigger retracts, X pushes."); }
            if (grab.Pressed) Debug.Log("[ShipbreakerVr] Grapple right-trigger press");
        }
        catch (System.Exception error)
        {
            failed = true; StopOwnedGrapple();
            Debug.LogError("[ShipbreakerVr] Grapple controls stopped after sampling failure: " + error);
        }
    }

    internal static Camera AimCamera()
    {
        if (instance) instance.Sample();
        return instance && instance.valid ? instance.aimCamera : LynxCameraController.MainCamera;
    }
    internal static Transform AimTransform()
    {
        var camera = AimCamera();
        return camera ? camera.transform : LynxCameraController.MainCameraTransform;
    }
    internal static Camera RaycastCamera(RaycastSystem system)
    {
        return system is GrappleRaycastSystem || system is GrapplePushRaycastSystem ? AimCamera() : LynxCameraController.MainCamera;
    }
    internal static bool Owns(LaserRope value)
    {
        if (instance) instance.Sample();
        return instance && instance.valid && instance.rope == value;
    }
    internal static bool AllowPush()
    {
        if (!instance || !instance.Controls) return true;
        instance.Sample();
        return instance.valid && Allowed(Action.Throw);
    }

    internal static bool WasPressed(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!instance || !instance.Controls) return controls.GetInputWasPressed(action);
        instance.Sample();
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.GrappleFire))) return instance.grab.Pressed && !instance.cancelTetherGrab;
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.RetractionModifier))) return instance.retract.Pressed;
        return instance.valid && controls.GetInputWasPressed(action);
    }
    internal static bool IsPressed(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!instance || !instance.Controls) return controls.GetInputIsPressed(action);
        instance.Sample();
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.GrappleFire))) return instance.grab.Held;
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.RetractionModifier))) return instance.retract.Held;
        return instance.valid && controls.GetInputIsPressed(action);
    }
    internal static bool WasReleased(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!instance || !instance.Controls) return controls.GetInputWasReleased(action);
        instance.Sample();
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.RetractionModifier))) return instance.valid && instance.retractReleased;
        return instance.valid && controls.GetInputWasReleased(action);
    }
    internal static void BeforeTetherUpdate(TetherController value)
    {
        if (!instance) return;
        instance.tethers = value;
        if (!instance.Controls) return;
        instance.Sample();
        if (value.State == TetherController.TetherState.Placing &&
            (!instance.valid || !instance.tracking.LeftHand.Grip.IsValid || !Allowed(Action.PlaceTether)))
            value.EquipmentChanged(); // Cancel, never interpret lost tracking as a release-to-place.
    }
    internal static bool TetherWasPressed(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!instance || !instance.Controls) return controls.GetInputWasPressed(action);
        instance.Sample();
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.PlaceTether))) return instance.tether.Pressed;
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.GrappleFire))) return instance.grab.Pressed;
        return instance.valid && controls.GetInputWasPressed(action);
    }
    internal static bool TetherWasReleased(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!instance || !instance.Controls) return controls.GetInputWasReleased(action);
        instance.Sample();
        return action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.PlaceTether))
            ? instance.valid && instance.tetherReleased : instance.valid && controls.GetInputWasReleased(action);
    }
    internal static Transform TetherBarrel(Transform original)
    {
        if (instance) instance.Sample();
        return instance && instance.valid ? instance.aimCamera.transform : original;
    }
    private void OnDisable() => StopOwnedGrapple();
    internal static void InputOwnerChanged()
    {
        if (!instance) return;
        // Cancel an interaction from either source before its aim origin changes.
        instance.owned = true;
        instance.StopOwnedGrapple();
        instance.sampledFrame = -1;
    }
    private void OnDestroy() { if (instance == this) instance = null; }
}
