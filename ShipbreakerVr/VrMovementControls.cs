using System;
using BBI.Unity.Game;
using BepInEx.Configuration;
using InControl;
using ShipbreakerVr.Tracking;
using UnityEngine;
using Action = BBI.Unity.Game.GameplayActions.GameplayActionSet;

namespace ShipbreakerVr;

internal sealed class VrMovementControls : MonoBehaviour
{
    private static VrMovementControls instance;
    private static ConfigEntry<bool> enabledSetting, swapSticks, invertPitch;
    private static ConfigEntry<float> deadzone, turnScale;
    private static ConfigEntry<VrButton> ascend, descend, rollLeft, rollRight, brakeExtra;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly MovementNeutralGate gate = new MovementNeutralGate();
    private readonly ShiftedStickGate shiftedStick = new ShiftedStickGate();
    private InputSystemUpdateBridge bridge;
    private Vector2 move, turn;
    private bool up, down, leftRoll, rightRoll, brake, failed;
    private int sampledFrame = -1;
    private string lastStatus;

    internal static void Configure(ConfigFile config)
    {
        enabledSetting = config.Bind("Movement", "Enabled", true, "VR movement using the game's thrusters and body rotation. F3-off restores original input.");
        swapSticks = config.Bind("Movement", "SwapSticks", false, "Swap move and turn sticks.");
        invertPitch = config.Bind("Movement", "InvertPitch", false, "Invert the turning stick's vertical axis.");
        deadzone = config.Bind("Movement", "StickDeadzone", .15f, new ConfigDescription("Ignore stick drift; the game's own deadzone also applies.", new AcceptableValueRange<float>(.05f, .5f)));
        turnScale = config.Bind("Movement", "TurnScale", 1f, new ConfigDescription("Scale input to the game's gamepad turning curve.", new AcceptableValueRange<float>(.1f, 1f)));
        ascend = config.Bind("Movement", "Ascend", VrButton.RightPrimary, "Default: right lower face button (Pico A / Frame compatibility A).");
        descend = config.Bind("Movement", "Descend", VrButton.RightSecondary, "Default: right upper face button (Pico B; Frame compatibility groups its top buttons).");
        rollLeft = config.Bind("Movement", "RollLeft", VrButton.LeftStickClick, "Default matches gamepad L3. Both roll buttons together brake.");
        rollRight = config.Bind("Movement", "RollRight", VrButton.RightStickClick, "Default matches gamepad R3. Both roll buttons together brake.");
        brakeExtra = config.Bind("Movement", "ExtraBrake", VrButton.None, "Optional additional single brake button, e.g. LeftGrip.");
    }

    private static bool Enabled => instance && instance.isActiveAndEnabled && enabledSetting.Value && VrInputMode.MotionActive;
    private static bool ContextAllowed => Enabled && GameSession.CurrentGameState == GameSession.GameState.Gameplay &&
        !EquipmentController.ToolMenuOpen && !VrMenuControls.PauseInputConsumed && Application.isFocused && LynxControls.Instance && LynxControls.Instance.IsGameFocused &&
        LynxControls.Instance.TryGetLoadedActionSet(LynxControls.PlayerActionSetTypes.GameplayActions)?.Enabled == true &&
        VrCamera.ViewCamera && VrCamera.ViewCamera.isActiveAndEnabled;

    private void Awake() => instance = this;
    internal static void InputOwnerChanged()
    {
        if (instance) { instance.ResetInput(); instance.sampledFrame = -1; }
    }
    private void Update() { if (!ContextAllowed) ResetInput(); }

    private void ResetInput()
    {
        move = turn = Vector2.zero;
        up = down = leftRoll = rightRoll = brake = false;
        gate.Sample(false, Vector2.zero, Vector2.zero, false, deadzone.Value);
        bridge?.Dispose();
        bridge = null;
        tracking.Clear();
    }

    private void Sample()
    {
        if (!ContextAllowed || failed) { ResetInput(); return; }
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        try
        {
            if (bridge == null) bridge = new InputSystemUpdateBridge();
            bridge.UpdateIfNeeded();
            tracking.Sample();
            var left = tracking.LeftHand.Inputs;
            var right = tracking.RightHand.Inputs;
            var leftMovement = shiftedStick.ForMovement(VrMenuControls.DpadShifted(left, right), left.Stick, deadzone.Value);
            var rawMove = swapSticks.Value ? right.Stick : leftMovement;
            var rawTurn = swapSticks.Value ? leftMovement : right.Stick;
            up = MovementInput.Button(ascend.Value, left, right);
            down = MovementInput.Button(descend.Value, left, right);
            leftRoll = MovementInput.Button(rollLeft.Value, left, right);
            rightRoll = MovementInput.Button(rollRight.Value, left, right);
            var extra = MovementInput.Button(brakeExtra.Value, left, right);
            var tracked = tracking.Head.IsValid && tracking.LeftHand.Grip.IsValid && tracking.RightHand.Grip.IsValid && left.StickAvailable && right.StickAvailable;
            var armed = gate.Sample(tracked, rawMove, rawTurn, up || down || leftRoll || rightRoll || extra, deadzone.Value);
            move = armed ? MovementInput.Deadzone(rawMove, deadzone.Value) : Vector2.zero;
            turn = armed ? MovementInput.Deadzone(rawTurn, deadzone.Value) * turnScale.Value : Vector2.zero;
            if (invertPitch.Value) turn.y = -turn.y;
            brake = armed && ((leftRoll && rightRoll) || extra);
            up &= armed; down &= armed; leftRoll &= armed && !extra; rightRoll &= armed && !extra;
            var status = !tracked ? "tracking/sticks unavailable" : !armed ? "center sticks and release movement buttons" : "ready: gamepad-style movement";
            if (lastStatus != status)
            {
                lastStatus = status;
                Debug.Log($"[ShipbreakerVr] Movement: {status}; leftStick={left.StickAvailable}; rightStick={right.StickAvailable}");
            }
        }
        catch (Exception error)
        {
            failed = true;
            ResetInput();
            Debug.LogError($"[ShipbreakerVr] Movement input stopped after sampling failure. F3-off restores original input. {error}");
        }
    }

    private static bool Allowed(LynxControls controls, Action action)
    {
        var id = LynxControls.ActionSetAndId.FromEnum(action);
        return controls.TryGetLoadedActionSet(LynxControls.PlayerActionSetTypes.GameplayActions)?.Enabled == true &&
            controls.TryGetLoadedPlayerAction(id)?.Enabled == true;
    }

    private static float Axis(LynxControls controls, float value, Action negative, Action positive) =>
        value < 0f ? (Allowed(controls, negative) ? value : 0f) : (Allowed(controls, positive) ? value : 0f);

    internal static float OneAxis(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!Enabled) return controls.GetOneAxisInputControlValue(action);
        instance.Sample();
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.ThrustMoveLeftRightComposite)))
            return Axis(controls, instance.move.x, Action.ThrustMoveLeft, Action.ThrustMoveRight);
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.ThrustMoveBackForwardComposite)))
            return Axis(controls, instance.move.y, Action.ThrustMoveBackward, Action.ThrustMoveForward);
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.ThrustMoveUpDownComposite)))
            return Axis(controls, (instance.up ? 1f : 0f) - (instance.down ? 1f : 0f), Action.ThrustMoveDown, Action.ThrustMoveUp);
        return controls.GetOneAxisInputControlValue(action);
    }

    internal static Vector2 TwoAxis(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!Enabled || !action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.RotateBodyComposite))) return controls.GetTwoAxisInputControlVector(action);
        instance.Sample();
        return new Vector2(Axis(controls, instance.turn.x, Action.RotateBodyLeft, Action.RotateBodyRight),
            Axis(controls, instance.turn.y, Action.RotateBodyDown, Action.RotateBodyUp));
    }

    internal static bool Pressed(LynxControls controls, LynxControls.ActionSetAndId action)
    {
        if (!Enabled) return controls.GetInputIsPressed(action);
        instance.Sample();
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.RollBodyLeft))) return instance.leftRoll && Allowed(controls, Action.RollBodyLeft);
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.RollBodyRight))) return instance.rightRoll && Allowed(controls, Action.RollBodyRight);
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.ThrustBrakeLeft))) return instance.brake && Allowed(controls, Action.ThrustBrakeLeft);
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.ThrustBrakeRight))) return instance.brake && Allowed(controls, Action.ThrustBrakeRight);
        // This keyboard modifier must not divert the VR turning stick to keyboard roll input.
        if (action.Equals(LynxControls.ActionSetAndId.FromEnum(Action.ModifiedRoll))) return false;
        return controls.GetInputIsPressed(action);
    }

    internal static BindingSourceType InputType(LynxControls controls) => Enabled ? BindingSourceType.DeviceBindingSource : controls.LastInputType;
    private void OnDisable() => ResetInput();
    private void OnDestroy() { if (instance == this) instance = null; }
}
