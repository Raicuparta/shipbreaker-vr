using System;
using HarmonyLib;
using InControl;
using ShipbreakerVr.Tracking;
using ShipbreakerVr;

internal static class InputModeChecks
{
    private sealed class Actions : PlayerActionSet
    {
        internal readonly PlayerAction Interact;
        internal readonly PlayerAction Trigger;
        internal Actions()
        {
            Interact = CreatePlayerAction("Interact");
            Interact.AddDefaultBinding(InputControlType.Action1);
            Trigger = CreatePlayerAction("Trigger");
            Trigger.AddDefaultBinding(InputControlType.RightTrigger);
        }
    }
    private sealed class Pad : InputDevice
    {
        internal bool Pressed;
        internal float TriggerValue;
        internal Pad() : base("Couch test pad")
        {
            AddControl(InputControlType.Action1, "A");
            AddControl(InputControlType.RightTrigger, "RT");
            // The real manager attaches devices before committing them. This also
            // allocates independent stick aliases instead of the shared Null ones.
            AccessTools.Method(typeof(InputDevice), "OnAttached").Invoke(this, null);
        }
        public override void Update(ulong tick, float dt)
        {
            UpdateWithState(InputControlType.Action1, Pressed, tick, dt);
            UpdateWithValue(InputControlType.RightTrigger, TriggerValue, tick, dt);
        }
    }
    internal static void Run(Action<bool, string> check)
    {
        var a = new InputActivity(2);
        check(!a.Observe(0, 1), "Hotplug with held button is not a mode switch");
        check(!a.Observe(0, 0), "Button release does not switch mode");
        check(a.Observe(0, 1), "Fresh button press requests switch");
        check(!a.Observe(0, 1), "Held button cannot steal mode each tick");
        check(!a.Observe(1, 0), "Neutral stick establishes baseline");
        check(!a.Observe(1, .12f) && !a.Observe(1, -.12f), "Stick drift ignored");
        check(a.Observe(1, .7f), "Deliberate stick deflection requests switch");
        check(!a.Observe(1, .5f) && !a.Observe(1, .7f), "Stick hysteresis rejects jitter");
        a.Observe(1, 0);
        check(a.Observe(1, -.8f), "Stick can switch again after recenter");
        check(!a.Observe(0, 1, false) && !a.Observe(0, 1), "Lost tracking and held reconnect do not switch");
        a.Reset();
        check(!a.Observe(1, 1), "Restoring focus does not reuse held stick");
        check(!a.Observe(1, float.NaN) && !a.Observe(1, float.PositiveInfinity), "Invalid axis ignored");
        var mode = new InputModeSelection();
        check(mode.Motion, "Existing motion mode remains default");
        check(!mode.Sample(VrInputMode.Auto, true, true, false, false, 0), "Connected idle pad cannot steal control");
        check(mode.Sample(VrInputMode.Auto, true, true, false, true, 1) && !mode.Motion, "Gamepad press selects couch mode");
        check(!mode.Sample(VrInputMode.Auto, true, true, false, false, 2) && !mode.Motion, "Tracked poses alone leave couch selected");
        check(mode.Sample(VrInputMode.Auto, true, true, true, false, 3) && mode.Motion, "Motion button restores motion mode");
        check(!mode.Sample(VrInputMode.Auto, true, true, false, true, 3.1f), "Delayed Steam Input echo does not steal motion mode");
        check(mode.Sample(VrInputMode.Auto, true, true, false, true, 4) && !mode.Motion, "Independent later gamepad press still works");
        check(mode.Sample(VrInputMode.Auto, true, true, true, true, 5) && mode.Motion, "Simultaneous XR/gamepad echo prefers tracked input");
        check(mode.Sample(VrInputMode.Gamepad, true, true, true, false, 6) && !mode.Motion, "Explicit couch override ignores motion buttons");
        check(mode.Sample(VrInputMode.MotionControllers, true, true, false, true, 7) && mode.Motion, "Explicit motion override ignores pad buttons");
        check(!mode.Sample(VrInputMode.Auto, false, false, true, true, 8), "Unavailable sources cannot request switch");
        check(mode.Sample(VrInputMode.Auto, false, true, false, true, 9) && !mode.Motion, "Couch mode works without tracked hands");
        check(AccessTools.Method(typeof(InputManager), "UpdateActiveDevice") != null, "Arbitration hook exists in shipped InControl");
        check(AccessTools.Field(typeof(InputManager), "activeDevice")?.FieldType == typeof(InputDevice), "Native active-device hook matches library");
        var pad = new Pad(); var activity = new InputActivity(1);
        void Tick(ulong tick) { pad.Update(tick, .016f); pad.Commit(tick, .016f); }
        Tick(1); check(!activity.Observe(0, pad.Action1.Value), "Committed neutral InControl pad creates baseline");
        pad.Pressed = true; Tick(2);
        check(activity.Observe(0, pad.Action1.Value) && pad.Action1.WasPressed, "Selection observes real committed button before action consumption");
        Tick(3); check(!activity.Observe(0, pad.Action1.Value), "Native held button remains stable");
        pad.Pressed = false; Tick(4);
        check(!activity.Observe(0, pad.Action1.Value) && pad.Action1.WasReleased, "Native release is preserved without claiming input");
        var active = AccessTools.Field(typeof(InputManager), "activeDevice");
        var previous = active.GetValue(null);
        var update = AccessTools.Method(typeof(PlayerActionSet), "Update");
        var actions = new Actions();
        try
        {
            active.SetValue(null, pad);
            void Consume(ulong tick) { Tick(tick); update.Invoke(actions, new object[] { tick, .016f }); }
            Consume(5);
            pad.Pressed = true; Consume(6);
            check(actions.Device == null && actions.Interact.WasPressed, "Unpinned native action consumes the selected couch gamepad");
            actions.Enabled = false; Consume(7);
            check(!actions.Interact.IsPressed, "Native interaction blocker is respected in couch mode");
            pad.Pressed = false; Consume(8);
            actions.Enabled = true; Consume(9);
            pad.Pressed = true; Consume(10);
            check(actions.Interact.WasPressed, "Native gamepad resumes after interaction blocker clears");
            pad.Pressed = false; Consume(11);
            actions.Destroy(); actions = new Actions(); Consume(12);
            pad.Pressed = true; Consume(13);
            check(actions.Device == null && actions.Interact.WasPressed, "Recreated action context automatically sees selected gamepad without device pinning");
            // Model Steam Input changing the active pad/slot while couch mode is
            // already armed. Its fresh analog input need not cross the mode-switch threshold.
            var other = new Pad();
            var devices = (System.Collections.Generic.List<InputDevice>)AccessTools.Field(typeof(InputManager), "devices").GetValue(null);
            var choose = AccessTools.Method(typeof(InputManager), "UpdateActiveDevice");
            devices.Add(pad); devices.Add(other);
            try
            {
                pad.Pressed = false; Tick(14); other.Update(14, .016f); other.Commit(14, .016f);
                other.TriggerValue = .4f; Tick(15); other.Update(15, .016f); other.Commit(15, .016f);
                choose.Invoke(null, null);
                check(ReferenceEquals(InputManager.ActiveDevice, other), "Native arbitration selects newly active gamepad on partial trigger input");
                active.SetValue(null, NativeGamepadRouting.Filter(InputManager.ActiveDevice, true, true));
                update.Invoke(actions, new object[] { (ulong)15, .016f });
                check(ReferenceEquals(InputManager.ActiveDevice, other) && actions.Trigger.Value > .1f,
                    "Armed couch mode preserves native gamepad reselection and delivers its input");
                active.SetValue(null, NativeGamepadRouting.Filter(InputManager.ActiveDevice, true, false));
                update.Invoke(actions, new object[] { (ulong)16, .016f });
                check(!actions.Trigger.IsPressed && actions.Trigger.Value == 0, "Only the unarmed handoff gate suppresses gamepad input");
                check(ReferenceEquals(NativeGamepadRouting.Filter(other, false, false), other), "Inactive couch mode leaves stock arbitration untouched");
            }
            finally { devices.Remove(pad); devices.Remove(other); }
        }
        finally { actions.Destroy(); active.SetValue(null, previous); }
    }
}
