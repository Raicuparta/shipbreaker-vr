using BBI.Unity.Game;
using BepInEx.Configuration;
using ShipbreakerVr.Tracking;
using UnityEngine;
using Action = BBI.Unity.Game.GameplayActions.GameplayActionSet;

namespace ShipbreakerVr;

// The additional tool adapter shares semantic poses/buttons, never a vendor profile.
internal sealed class VrAdditionalToolControls : MonoBehaviour
{
    private static VrAdditionalToolControls instance;
    private static ConfigEntry<bool> enabledSetting;
    private EquipmentController equipment;
    private CuttingToolController cutter;
    private Camera aimCamera;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly TriggerLatch primary = new TriggerLatch(), alternate = new TriggerLatch();
    private InputSystemUpdateBridge bridge;
    private int sampledFrame = -1, sampledMode = -1;
    private bool valid, failed;
    private string lastStatus;
    private int Mode => !equipment ? 0 : equipment.CurrentEquipment == EquipmentController.Equipment.DemoCharge ? 2 :
        equipment.CurrentEquipment == EquipmentController.Equipment.Scanner ? 3 :
        equipment.CurrentEquipment == EquipmentController.Equipment.CuttingTool && cutter && cutter.CurrentMode == CuttingToolController.CutterMode.Cutter ? 1 : 0;
    private bool Controls => enabledSetting.Value && VrInputMode.MotionActive && Mode != 0;
    private bool Context => Controls && !failed && Application.isFocused && LynxControls.Instance && LynxControls.Instance.IsGameFocused &&
        GameSession.CurrentGameState == GameSession.GameState.Gameplay && !EquipmentController.ToolMenuOpen && !VrMenuControls.PauseInputConsumed &&
        LynxControls.Instance.TryGetLoadedActionSet(LynxControls.PlayerActionSetTypes.GameplayActions)?.Enabled == true && VrCamera.BodyTransform && VrCamera.ViewCamera;

    internal static void Configure(ConfigFile config) => enabledSetting = config.Bind("Controllers", "AdditionalToolMotionControls", true,
        "Splitsaw and demo-charge hand aim, right-trigger primary, left-trigger angle; Pico scanner triggers or Frame bumpers cycle modes. Native tool rules stay active.");
    private void Awake()
    {
        instance = this;
        var host = new GameObject("ShipbreakerVr additional tool projection");
        host.transform.SetParent(transform, false);
        aimCamera = host.AddComponent<Camera>();
        aimCamera.enabled = false; aimCamera.stereoTargetEye = StereoTargetEyeMask.None;
    }
    internal static void RegisterEquipment(EquipmentController value)
    {
        if (!instance || instance.equipment == value) return;
        instance.Reset(); instance.equipment = value; instance.sampledFrame = -1;
    }
    internal static void RegisterCutter(CuttingToolController value)
    {
        if (!instance) return;
        instance.cutter = value; RegisterEquipment(value.EquipementController);
    }
    // A placement/detonator transition cannot reuse a held fire trigger.
    internal static void DemoStateChanged(DemoChargeState state)
    {
        if (instance && (state == DemoChargeState.Placement || state == DemoChargeState.Detonation))
        { instance.Reset(); instance.sampledFrame = -1; }
    }
    private static bool Allowed(Action action) => LynxControls.Instance &&
        LynxControls.Instance.TryGetLoadedPlayerAction(LynxControls.ActionSetAndId.FromEnum(action))?.Enabled == true;
    private void Reset()
    {
        valid = false; primary.Sample(false, 0f); alternate.Sample(false, 0f);
        bridge?.Dispose(); bridge = null; tracking.Clear();
    }
    private void Update() { if (!Context || sampledMode != Mode) Reset(); }
    private void Sample()
    {
        if (!Context) { Reset(); return; }
        var mode = Mode;
        if (sampledMode != mode) { Reset(); sampledMode = mode; sampledFrame = -1; }
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        try
        {
            if (bridge == null) bridge = new InputSystemUpdateBridge();
            bridge.UpdateIfNeeded(); tracking.Sample();
            valid = tracking.Head.IsValid && tracking.RightHand.Aim.IsValid && tracking.RightHand.Grip.IsValid;
            if (!valid) { Reset(); return; }
            var body = VrCamera.BodyTransform;
            var pose = ShipbreakerTrackingSpace.ToWorld(tracking.Head, tracking.RightHand.Aim, body.position, body.rotation, VrCamera.EyeOffset);
            var source = LynxCameraController.MainCamera;
            if (source) aimCamera.CopyFrom(source);
            aimCamera.enabled = false; aimCamera.stereoTargetEye = StereoTargetEyeMask.None; aimCamera.targetTexture = null;
            aimCamera.transform.SetPositionAndRotation(pose.position, pose.rotation); aimCamera.ResetWorldToCameraMatrix();
            var fire = mode == 1 ? Action.CutterFire : mode == 2 ? Action.DemoChargeFire : Action.ScanCycleRight;
            var alt = mode == 1 ? Action.CutterAltFire : mode == 2 ? Action.DemoChargeAltFire : Action.ScanCycleLeft;
            var gamepadScanner = mode == 3 && tracking.LeftHand.Inputs.FullGamepad && tracking.RightHand.Inputs.FullGamepad;
            primary.Sample(Allowed(fire) && (mode != 1 || !cutter.IsCutterFireActionBlocked),
                gamepadScanner ? (tracking.RightHand.Inputs.Bumper ? 1f : 0f) : tracking.RightHand.Trigger);
            alternate.Sample(Allowed(alt) && tracking.LeftHand.Grip.IsValid,
                gamepadScanner ? (tracking.LeftHand.Inputs.Bumper ? 1f : 0f) : tracking.LeftHand.Trigger);
            var status = mode == 1 ? "Splitsaw" : mode == 2 ? "Demo charge" : "Scanner";
            if (status != lastStatus) { lastStatus = status; Debug.Log("[ShipbreakerVr] Additional tool ready: " + status); }
            if (primary.Pressed || alternate.Pressed) Debug.Log($"[ShipbreakerVr] {status} trigger: primary={primary.Pressed}, alternate={alternate.Pressed}");
        }
        catch (System.Exception error) { failed = true; Reset(); Debug.LogError("[ShipbreakerVr] Additional tool input stopped: " + error); }
    }
    internal static bool WasPressed(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!instance || !instance.Controls) return controls.GetInputWasPressed(action);
        instance.Sample();
        var mode = instance.Mode;
        // Scanner's native optional ToolMode alias duplicates the dedicated trigger
        // actions via right grip (or shifted D-pad). Keep mode grips for other tools.
        if (mode == 3 && action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.ToolMode))) return false;
        var fire = mode == 1 ? Action.CutterFire : mode == 2 ? Action.DemoChargeFire : Action.ScanCycleRight;
        var alt = mode == 1 ? Action.CutterAltFire : mode == 2 ? Action.DemoChargeAltFire : Action.ScanCycleLeft;
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(fire))) return instance.primary.Pressed;
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(alt))) return instance.alternate.Pressed;
        return instance.valid && controls.GetInputWasPressed(action);
    }
    internal static Camera AimCamera()
    {
        if (instance) instance.Sample();
        return instance && instance.valid ? instance.aimCamera : LynxCameraController.MainCamera;
    }
    internal static Transform AimTransform()
    {
        var camera = AimCamera(); return camera ? camera.transform : LynxCameraController.MainCameraTransform;
    }
    internal static Ray ScreenRay(Camera camera, Vector3 pixel)
    {
        var size = new Vector2(LynxCameraController.ScreenWidth, LynxCameraController.ScreenHeight);
        if (instance && instance.valid && camera == instance.aimCamera)
            return new Ray(camera.transform.position, camera.transform.rotation * ToolProjection.Direction(pixel, size, camera.fieldOfView, camera.aspect));
        if (ModXrManager.IsVrEnabled && (camera == VrCamera.ViewCamera || (VrInputMode.CouchActive && camera == LynxCameraController.MainCamera)) && size.x > 0 && size.y > 0)
            return camera.ViewportPointToRay(new Vector3(pixel.x / size.x, pixel.y / size.y, 0), Camera.MonoOrStereoscopicEye.Mono);
        return camera.ScreenPointToRay(pixel);
    }
    internal static bool TryDemoAim(out Pose pose)
    {
        pose = default;
        if (!instance || !instance.Controls || instance.Mode != 2) return false;
        instance.Sample();
        if (!instance.valid) return false;
        pose = new Pose(instance.aimCamera.transform.position, instance.aimCamera.transform.rotation);
        return true;
    }
    internal static Vector3 BeamOrigin(Transform original, Vector3 offset)
    {
        if (instance) instance.Sample();
        return instance && instance.valid && instance.Mode == 1 ? VrAvatarVisuals.ToolBeamOrigin(EquipmentController.Equipment.CuttingTool, new Pose(instance.aimCamera.transform.position, instance.aimCamera.transform.rotation)) : original.TransformPoint(offset);
    }
    internal static Vector3 BeamPosition(Transform original)
    {
        if (instance) instance.Sample();
        return instance && instance.valid && instance.Mode == 1 ? VrAvatarVisuals.ToolBeamOrigin(EquipmentController.Equipment.CuttingTool, new Pose(instance.aimCamera.transform.position, instance.aimCamera.transform.rotation)) : original.position;
    }
    internal static bool SplitsawActive { get { if (instance) instance.Sample(); return instance && instance.valid && instance.Mode == 1; } }
    internal static bool AllowCut()
    {
        if (!instance || !instance.Controls || instance.Mode != 1) return true;
        instance.Sample(); return instance.valid;
    }
    internal static void DemoTransform(ref Transform result)
    {
        if (!instance || !instance.Controls || instance.Mode != 2) return;
        instance.Sample(); if (instance.valid) result = instance.aimCamera.transform;
    }
    internal static Camera ScannerCamera() => ModXrManager.IsVrEnabled && VrCamera.ViewCamera ? VrCamera.ViewCamera : LynxCameraController.MainCamera;
    private void OnDisable() => Reset();
    internal static void InputOwnerChanged()
    {
        if (instance) { instance.Reset(); instance.sampledFrame = -1; }
    }
    private void OnDestroy() { if (instance == this) instance = null; }
}
