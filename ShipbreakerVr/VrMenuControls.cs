using System;
using BBI.Unity.Game;
using BepInEx.Configuration;
using HarmonyLib;
using InControl;
using ShipbreakerVr.Tracking;
using UnityEngine;

namespace ShipbreakerVr;

// Feeds InControl before it commits devices and updates PlayerActions. The game's
// bindings, action locks, repeat timing and tool wheel remain authoritative.
[HarmonyPatch]
internal sealed class VrMenuControls : MonoBehaviour
{
    private static VrMenuControls instance;
    private static ConfigEntry<bool> enabledSetting;
    private static ConfigEntry<bool> verboseInput;
    private static ConfigEntry<float> deadzone;
    private static ConfigEntry<VrButton> confirm, back, interact, tools, mode, pauseFirst, pauseSecond, previousTab, nextTab, misc1, misc2;
    private static ConfigEntry<VrButton> selectFirst, selectSecond, dpadModifier;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly MenuNeutralGate gate = new MenuNeutralGate();
    private readonly ButtonChord pauseChord = new ButtonChord();
    private readonly ButtonChord selectChord = new ButtonChord();
    private readonly ChordMemberDelay selectDelayA = new ChordMemberDelay(), selectDelayB = new ChordMemberDelay();
    private readonly TriggerLatch menuLeftTrigger = new TriggerLatch(), menuRightTrigger = new TriggerLatch();
    internal static bool PauseInputConsumed => ModXrManager.ToggleInputConsumed || (instance && instance.Eligible && (instance.pauseChord.ConsumesButtons || instance.selectChord.ConsumesButtons ||
        (instance.rawLeft.FullGamepad && instance.rawRight.FullGamepad && (instance.rawLeft.View || instance.rawRight.Menu))));
    internal static bool DpadShifted(ControllerInputs left, ControllerInputs right) => !left.FullGamepad && instance && instance.Eligible && MovementInput.Button(dpadModifier.Value, left, right);
    private InputSystemUpdateBridge bridge;
    private MenuDevice device;
    private PlayerActionSet pinnedActions;
    private InputDevice previousActionDevice;
    private InputDevice assignedActionDevice;
    private bool canRoute;
    private MenuInput routed;
    private ControllerInputs rawLeft, rawRight;
    private string lastInputReport;
    private bool failed, wheelOwned, cancelWheel;
    private string lastStatus;
    private bool? lastFullGamepad;
    private static readonly System.Reflection.MethodInfo closeWheel = AccessTools.Method(typeof(EquipmentController), "CloseToolSelectMenu");

    internal static void Configure(ConfigFile config)
    {
        verboseInput = config.Bind("Diagnostics", "VerboseMenuInput", false, "Log detailed menu input and native bindings on input changes. Enable only for input troubleshooting.");
        enabledSetting = config.Bind("Menus", "Enabled", true, "VR tool selection and gamepad-style menu navigation. Does not add laser clicking.");
        deadzone = config.Bind("Menus", "StickDeadzone", .25f, new ConfigDescription("Menu stick deadzone.", new AcceptableValueRange<float>(.1f, .5f)));
        confirm = config.Bind("Menus", "Confirm", VrButton.RightPrimary, "Menus: confirm/continue (Pico A).");
        back = config.Bind("Menus", "Back", VrButton.RightSecondary, "Menus: back/decline (Pico B).");
        interact = config.Bind("Menus", "Interact", VrButton.LeftPrimary, "Gameplay: interact (Pico X).");
        tools = config.Bind("Menus", "ToolWheel", VrButton.LeftSecondary, "Gameplay: hold, choose with RIGHT stick, release to equip (Pico Y).");
        mode = config.Bind("Menus", "ToolMode", VrButton.RightGrip, "Gameplay: change cutter or demo-charge mode.");
        // New keys intentionally replace the old single-button PauseStart binding.
        pauseFirst = config.Bind("Menus", "PauseChordFirst", VrButton.RightSecondary, "Pause/resume or Start: press BOTH chord buttons (Pico B + Y). Release both before repeating.");
        pauseSecond = config.Bind("Menus", "PauseChordSecond", VrButton.LeftSecondary, "Second pause chord button. Must differ from the first; None disables the chord.");
        selectFirst = config.Bind("Menus", "SelectChordFirst", VrButton.LeftPrimary, "Select/View: both chord buttons together (Pico X + Y).");
        selectSecond = config.Bind("Menus", "SelectChordSecond", VrButton.LeftSecondary, "Second Select/View button; release both before repeating.");
        dpadModifier = config.Bind("Menus", "DpadModifier", VrButton.LeftGrip, "Gameplay: hold with LEFT stick for four-way D-pad input. Menus use grips as LB/RB. Recenter before movement resumes.");
        previousTab = config.Bind("Menus", "MenuLeftBumper", VrButton.LeftGrip, "Menus/options: Xbox LB / previous tab (left grip).");
        nextTab = config.Bind("Menus", "MenuRightBumper", VrButton.RightGrip, "Menus/options: Xbox RB / next tab (right grip).");
        misc1 = config.Bind("Menus", "MenuY", VrButton.LeftSecondary, "Menus: contextual Xbox Y (Pico Y).");
        misc2 = config.Bind("Menus", "MenuX", VrButton.LeftPrimary, "Menus: contextual Xbox X / defaults (Pico X).");
    }

    private bool Eligible => isActiveAndEnabled && enabledSetting.Value && !failed && VrInputMode.MotionActive && !ModXrManager.ToggleInputConsumed &&
        Application.isFocused && LynxControls.Instance && LynxControls.Instance.IsGameFocused;

    internal static bool VerboseInputEnabled => verboseInput != null && verboseInput.Value;

    internal static bool ControllerNavigationActive => (VrInputMode.CouchActive || (instance && instance.Eligible && instance.canRoute)) &&
        (GameSession.CurrentGameState != GameSession.GameState.Gameplay || EquipmentController.ToolMenuOpen);

    // Head movement changes screen-to-world mouse hover even when the physical
    // mouse is stationary. Leave navigation/submit processing intact; suppress only
    // the mouse path in the game's input module while VR controllers own menus.
    [HarmonyPrefix, HarmonyPatch(typeof(UnityEngine.EventSystems.StandaloneInputModule), "ProcessMouseEvent", new[] { typeof(int) })]
    private static bool MouseHover(UnityEngine.EventSystems.StandaloneInputModule __instance) =>
        !(__instance is LynxInputModule) || !ControllerNavigationActive;

    private void Awake() => instance = this;
    private void OnEnable() => InputManager.OnUpdate += ReportInput;
    private void Update()
    {
        if (InputManager.IsSetup && (device == null || !device.IsAttached))
        {
            if (device == null) device = new MenuDevice(this);
            InputManager.AttachDevice(device);
            InputManager.OnUpdate -= ReportInput;
            InputManager.OnUpdate += ReportInput;
            Debug.Log("[ShipbreakerVr] VR menu/tool gamepad attached; game bindings and action locks retained.");
        }
        if (!Eligible) ResetInput();
    }

    private void ResetInput()
    {
        canRoute = false;
        if (ReferenceEquals(assignedActionDevice, device)) ReleaseActionDevice();
        gate.Sample((int)GameSession.CurrentGameState, false, Vector2.zero, Vector2.zero, false, deadzone.Value);
        cancelWheel |= wheelOwned;
        pauseChord.Sample(false, false, false);
        selectChord.Sample(false, false, false);
        selectDelayA.Sample(false, false, false, 0f); selectDelayB.Sample(false, false, false, 0f);
        menuLeftTrigger.Sample(false, 0f); menuRightTrigger.Sample(false, 0f);
        tracking.Clear();
        rawLeft = rawRight = default;
        bridge?.Dispose();
        bridge = null;
    }

    private MenuInput Sample()
    {
        canRoute = false;
        if (!Eligible) { ResetInput(); return default; }
        try
        {
            if (bridge == null) bridge = new InputSystemUpdateBridge();
            bridge.UpdateIfNeeded();
            tracking.Sample();
            var left = tracking.LeftHand.Inputs;
            var right = tracking.RightHand.Inputs;
            rawLeft = left; rawRight = right;
            var tracked = tracking.Head.IsValid && tracking.LeftHand.Grip.IsValid && tracking.RightHand.Grip.IsValid && left.StickAvailable && right.StickAvailable;
            var fullGamepad = left.FullGamepad && right.FullGamepad;
            if (lastFullGamepad != fullGamepad)
            {
                gate.Sample((int)GameSession.CurrentGameState, false, left.Stick, right.Stick, true, deadzone.Value);
                cancelWheel |= wheelOwned;
                pauseChord.Sample(false, false, false); selectChord.Sample(false, false, false);
                selectDelayA.Sample(false, false, false, 0f); selectDelayB.Sample(false, false, false, 0f);
                lastFullGamepad = fullGamepad;
                Debug.Log("[ShipbreakerVr] Button layout: " + (fullGamepad ? "native split gamepad (ABXY/D-pad/LB/RB/Menu/View)" : "legacy VR controller bindings"));
            }
            if (fullGamepad)
            {
                var gamepadReady = gate.Sample((int)GameSession.CurrentGameState, tracked, left.Stick, right.Stick,
                    left.AnyButton || right.AnyButton || tracking.LeftHand.Trigger > .25f || tracking.RightHand.Trigger > .25f, deadzone.Value);
                menuLeftTrigger.Sample(gamepadReady && GameSession.CurrentGameState != GameSession.GameState.Gameplay, tracking.LeftHand.Trigger);
                menuRightTrigger.Sample(gamepadReady && GameSession.CurrentGameState != GameSession.GameState.Gameplay, tracking.RightHand.Trigger);
                if (!gamepadReady) { cancelWheel |= wheelOwned; return default; }
                canRoute = true;
                var gamepad = MenuInput.Gamepad(GameSession.CurrentGameState == GameSession.GameState.Gameplay,
                    EquipmentController.ToolMenuOpen, left, right, deadzone.Value, menuLeftTrigger.Held, menuRightTrigger.Held);
                if (gamepad.Start || gamepad.Select) cancelWheel |= wheelOwned;
                if (gamepad.Tools) wheelOwned = true;
                else if (!EquipmentController.ToolMenuOpen) { wheelOwned = false; cancelWheel = false; }
                return gamepad;
            }
            bool Read(ConfigEntry<VrButton> setting) => MovementInput.Button(setting.Value, left, right);
            var a = Read(confirm); var b = Read(back); var x = Read(interact); var y = Read(tools);
            var m = Read(mode); var first = Read(pauseFirst); var second = Read(pauseSecond);
            var p = Read(previousTab); var n = Read(nextTab);
            var extra1 = Read(misc1); var extra2 = Read(misc2);
            var selectA = Read(selectFirst); var selectB = Read(selectSecond); var shift = Read(dpadModifier);
            var ready = gate.Sample((int)GameSession.CurrentGameState, tracked, left.Stick, right.Stick,
                a || b || x || y || m || first || second || p || n || extra1 || extra2 || selectA || selectB || shift || tracking.LeftHand.Trigger > .25f || tracking.RightHand.Trigger > .25f, deadzone.Value);
            pauseChord.Sample(ready && ButtonChord.ValidBinding(pauseFirst.Value, pauseSecond.Value), first, second);
            selectChord.Sample(ready && !pauseChord.ConsumesButtons && ButtonChord.ValidBinding(selectFirst.Value, selectSecond.Value), selectA, selectB);
            var status = !tracked ? "tracking/sticks unavailable" : !ready ? "center sticks and release buttons" : "ready in " + GameSession.CurrentGameState;
            if (lastStatus != status) { lastStatus = status; Debug.Log("[ShipbreakerVr] Menu controls: " + status); }
            menuLeftTrigger.Sample(ready && GameSession.CurrentGameState != GameSession.GameState.Gameplay, tracking.LeftHand.Trigger);
            menuRightTrigger.Sample(ready && GameSession.CurrentGameState != GameSession.GameState.Gameplay, tracking.RightHand.Trigger);
            var consumed = pauseChord.ConsumesButtons || selectChord.ConsumesButtons;
            var delayedA = selectDelayA.Sample(ready, selectA, consumed, Time.unscaledTime);
            var delayedB = selectDelayB.Sample(ready, selectB, consumed, Time.unscaledTime);
            if (!ready) { cancelWheel |= wheelOwned; return default; }
            bool Defer(ConfigEntry<VrButton> setting, bool raw) => setting.Value == selectFirst.Value ? delayedA : setting.Value == selectSecond.Value ? delayedB : raw;
            a = Defer(confirm, a); b = Defer(back, b); x = Defer(interact, x); y = Defer(tools, y);
            m = Defer(mode, m); p = Defer(previousTab, p); n = Defer(nextTab, n);
            extra1 = Defer(misc1, extra1); extra2 = Defer(misc2, extra2);
            canRoute = true;
            if (pauseChord.ConsumesButtons)
            {
                cancelWheel |= wheelOwned;
                return new MenuInput { Start = pauseChord.Held };
            }
            if (selectChord.ConsumesButtons)
            {
                cancelWheel |= wheelOwned;
                return new MenuInput { Select = selectChord.Held };
            }
            var result = MenuInput.Route(GameSession.CurrentGameState == GameSession.GameState.Gameplay,
                EquipmentController.ToolMenuOpen, MovementInput.Deadzone(left.Stick, deadzone.Value),
                MovementInput.Deadzone(right.Stick, deadzone.Value), a, b, x, y, m, false, p, n, extra1, extra2, false, shift, menuLeftTrigger.Held, menuRightTrigger.Held);
            if (result.Tools) wheelOwned = true;
            else if (!EquipmentController.ToolMenuOpen) { wheelOwned = false; cancelWheel = false; }
            return result;
        }
        catch (Exception error)
        {
            failed = true;
            ResetInput();
            Debug.LogError("[ShipbreakerVr] Menu controls stopped after input failure: " + error);
            return default;
        }
    }

    private void OnDisable()
    {
        InputManager.OnUpdate -= ReportInput;
        ResetInput();
        ReleaseActionDevice();
        // Neutral device stays attached during VR toggles; no unplug dialog.
    }
    private void OnDestroy()
    {
        if (device != null && InputManager.IsSetup) { device.Passive = true; InputManager.DetachDevice(device); }
        if (instance == this) instance = null;
    }

    private void ReleaseActionDevice()
    {
        if (pinnedActions != null && ReferenceEquals(pinnedActions.Device, assignedActionDevice))
            pinnedActions.Device = previousActionDevice;
        pinnedActions = null;
        previousActionDevice = null;
        assignedActionDevice = null;
    }

    internal static bool IsVirtualDevice(InputDevice value) => instance && ReferenceEquals(instance.device, value);
    internal static void InputOwnerChanged()
    {
        if (!instance) return;
        // Cancel the wheel before releasing its held action: a synthetic release
        // must not equip a different tool during a mode change.
        if (EquipmentController.ToolMenuOpen && VrToolPresentation.Equipment)
            closeWheel.Invoke(VrToolPresentation.Equipment, new object[] { true });
        instance.pinnedActions?.ClearInputState();
        instance.ResetInput();
        instance.device?.ClearInputState();
    }
    internal static void RefreshInputOwner()
    {
        if (!instance) return;
        if (!VrInputMode.MotionActive)
        {
            instance.ResetInput();
            instance.device?.ClearInputState();
        }
        if (instance.device != null) instance.device.Passive = !instance.Eligible || !instance.canRoute;
        instance.BindActionDevice();
    }

    private void BindActionDevice()
    {
        // Native pads already flow through InputManager.ActiveDevice. Only the
        // synthetic motion pad needs a pinned action source. Let native context
        // changes (interactions, doors, pause) manage their action sets normally.
        if (VrInputMode.CouchActive) { ReleaseActionDevice(); return; }
        var state = GameSession.CurrentGameState;
        var type = state == GameSession.GameState.Gameplay ? LynxControls.PlayerActionSetTypes.GameplayActions :
            state == GameSession.GameState.Paused ? LynxControls.PlayerActionSetTypes.PausedActions :
            state == GameSession.GameState.NIS ? LynxControls.PlayerActionSetTypes.NISActions : LynxControls.PlayerActionSetTypes.FEActions;
        var target = device;
        var actions = canRoute && LynxControls.Instance ? LynxControls.Instance.TryGetLoadedActionSet(type) : null;
        if (ReferenceEquals(actions, pinnedActions) && ReferenceEquals(target, assignedActionDevice) &&
            (actions == null || ReferenceEquals(actions.Device, target))) return;
        ReleaseActionDevice();
        if (actions == null) return;
        pinnedActions = actions;
        previousActionDevice = actions.Device;
        assignedActionDevice = target;
        actions.Device = target;
        Debug.Log($"[ShipbreakerVr] Menu action source: {type} uses {target?.Name}; enabled={actions.Enabled}");
    }

    // Observe AFTER the native device commit and PlayerAction update, not just pose readiness.
    private void ReportInput(ulong tick, float deltaTime)
    {
        if (!verboseInput.Value) return;
        if (!Eligible || device == null) return;
        var key = $"{GameSession.CurrentGameState}:{canRoute}:{rawLeft.Primary}/{rawLeft.Secondary}/{rawLeft.Grip}:{rawRight.Primary}/{rawRight.Secondary}/{rawRight.Grip}:" +
            $"{Math.Sign(routed.Navigate.x)},{Math.Sign(routed.Navigate.y)}:{routed.Tools}/{routed.Confirm}/{routed.Back}/{routed.Start}:Select={routed.Select}:Dpad={routed.Dpad.x},{routed.Dpad.y}:LT/RT={routed.LeftTrigger}/{routed.RightTrigger}";
        if (key == lastInputReport) return;
        lastInputReport = key;
        var evidence = "";
        if (pinnedActions != null)
            foreach (var action in pinnedActions.Actions)
                if (action.Name == "ToolMenu" || action.Name == "Confirm" || action.Name == "Back" || action.Name == "Pause" || action.Name == "Unpause" || action.Name == "NavigateUp" || action.Name == "WorkOrder" || action.Name == "PlayAudioLog" || action.Name == "PreviousTab" || action.Name == "NextTab")
                {
                    var bindings = new System.Collections.Generic.List<string>();
                    foreach (var binding in action.UnfilteredBindings)
                        bindings.Add(binding is DeviceBindingSource source ? source.Control.ToString() : binding.BindingSourceType.ToString());
                    evidence += $" {action.Name}={action.Value:F1}/enabled={action.Enabled}/bindings={string.Join(",", bindings)};";
                }
        Debug.Log($"[ShipbreakerVr] Menu input tick={tick}; raw/state={key}; padA={device.Action1.Value:F1}; padX={device.Action3.Value:F1}; padY={device.Action4.Value:F1}; padView={device.GetControl(InputControlType.View).Value:F1}; padBack={device.GetControl(InputControlType.Back).Value:F1}; padLB={device.LeftBumper.Value:F1}; padRB={device.RightBumper.Value:F1}; activePad={InputManager.ActiveDevice.Name};{evidence}");
    }

    [HarmonyPatch(typeof(EquipmentController), "Update"), HarmonyPrefix]
    private static void BeforeEquipmentUpdate(EquipmentController __instance)
    {
        if (instance && instance.wheelOwned && EquipmentController.ToolMenuOpen && (!instance.Eligible || instance.cancelWheel))
            closeWheel.Invoke(__instance, new object[] { true });
    }

    [HarmonyPatch(typeof(EquipmentController), "CloseToolSelectMenu"), HarmonyPrefix]
    private static void BeforeClose(ref bool closeWithoutSwap)
    {
        if (instance && instance.wheelOwned && (!instance.Eligible || instance.cancelWheel || GameSession.CurrentGameState != GameSession.GameState.Gameplay))
            closeWithoutSwap = true;
    }

    [HarmonyPatch(typeof(EquipmentController), "CloseToolSelectMenu"), HarmonyPostfix]
    private static void AfterClose()
    {
        if (instance) { instance.wheelOwned = false; instance.cancelWheel = false; }
    }

    private sealed class MenuDevice : InputDevice
    {
        private readonly VrMenuControls owner;
        internal MenuDevice(VrMenuControls owner) : base("Shipbreaker VR menu controls", true)
        {
            this.owner = owner;
            DeviceClass = InputDeviceClass.Controller;
            DeviceStyle = InputDeviceStyle.XboxOne;
            foreach (var control in new[] { InputControlType.LeftStickUp, InputControlType.LeftStickDown,
                InputControlType.LeftStickLeft, InputControlType.LeftStickRight, InputControlType.RightStickUp,
                InputControlType.RightStickDown, InputControlType.RightStickLeft, InputControlType.RightStickRight,
                InputControlType.Action1, InputControlType.Action2, InputControlType.Action3, InputControlType.Action4,
                InputControlType.Start, InputControlType.Back, InputControlType.Menu, InputControlType.View,
                InputControlType.LeftBumper, InputControlType.RightBumper, InputControlType.LeftTrigger, InputControlType.RightTrigger,
                InputControlType.DPadUp, InputControlType.DPadDown, InputControlType.DPadLeft, InputControlType.DPadRight })
                AddControl(control, control.ToString());
        }

        public override void Update(ulong updateTick, float deltaTime)
        {
            var input = owner ? owner.Sample() : default;
            if (owner) { owner.routed = input; owner.BindActionDevice(); }
            UpdateLeftStickWithValue(input.Navigate, updateTick, deltaTime);
            UpdateRightStickWithValue(input.Scroll, updateTick, deltaTime);
            void Set(InputControlType control, bool value) => UpdateWithState(control, value, updateTick, deltaTime);
            Set(InputControlType.LeftTrigger, input.LeftTrigger);
            Set(InputControlType.RightTrigger, input.RightTrigger);
            Set(InputControlType.Action1, input.Confirm);
            Set(InputControlType.Action2, input.Back);
            Set(InputControlType.Action3, input.Interact || input.Misc2);
            Set(InputControlType.Action4, input.Tools || input.Misc1);
            Set(InputControlType.Back, input.Back || input.Select);
            Set(InputControlType.View, input.Back || input.Select);
            Set(InputControlType.Start, input.Start);
            Set(InputControlType.Menu, input.Start);
            Set(InputControlType.LeftBumper, input.PreviousTab);
            Set(InputControlType.RightBumper, input.NextTab);
            Set(InputControlType.DPadRight, input.Mode || input.Dpad.x > 0f);
            Set(InputControlType.DPadUp, input.Dpad.y > 0f);
            Set(InputControlType.DPadDown, input.Dpad.y < 0f);
            Set(InputControlType.DPadLeft, input.Dpad.x < 0f);
        }
    }
}
