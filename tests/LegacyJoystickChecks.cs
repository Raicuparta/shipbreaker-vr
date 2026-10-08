using System;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using InControl;
using ShipbreakerVr;

internal static class LegacyJoystickChecks
{
    private sealed class Pad : InputDevice
    {
        internal bool Pressed;
        internal Pad() : base("Input regression test") { AddControl(InputControlType.Action1, "A"); }
        public override void Update(ulong tick, float dt) => UpdateWithState(InputControlType.Action1, Pressed, tick, dt);
    }
    // Unity native Input calls cannot be JIT-compiled/detoured by desktop CLR.
    // Route real library virtual reads through production prefixes; update/commit
    // and pending-tick enforcement still execute the shipped InControl code.
    private sealed class OverflowDevice : UnityInputDevice
    {
        internal bool Guarded;
        internal OverflowDevice() : base(11, "Overflow controller") { }
        public override bool ReadRawButtonState(int index)
        {
            bool result = true;
            if (Guarded && !LegacyJoystickSafety.Button(this, index, ref result)) return result;
            return (new bool[MaxDevices, MaxButtons])[JoystickId - 1, index];
        }
        public override float ReadRawAnalogValue(int index)
        {
            float result = 1;
            if (Guarded && !LegacyJoystickSafety.Analog(this, index, ref result)) return result;
            return (new float[MaxDevices, MaxAnalogs])[JoystickId - 1, index];
        }
    }
    private static void Tick(ulong tick, params InputDevice[] devices)
    {
        foreach (var device in devices) device.Update(tick, .016f);
        foreach (var device in devices) device.Commit(tick, .016f);
    }
    internal static void Run(Action<bool, string> check)
    {
        check(AccessTools.Method(typeof(UnityInputDeviceManager), "DetectJoystickDevice", new[] { typeof(int), typeof(string) }) != null, "Native device-detection hook signature exists");
        check(AccessTools.Method(typeof(UnityInputDevice), "ReadRawButtonState", new[] { typeof(int) }) != null, "Native button hook signature exists");
        check(AccessTools.Method(typeof(UnityInputDevice), "ReadRawAnalogValue", new[] { typeof(int) }) != null, "Native analog hook signature exists");
        var slot = typeof(UnityInputDevice).GetField("<JoystickId>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var id in new[] { -1, 0, 11, 16, int.MaxValue })
        {
            var device = (UnityInputDevice)FormatterServices.GetUninitializedObject(typeof(UnityInputDevice));
            slot.SetValue(device, id);
            bool button = true; float axis = 1;
            check(!LegacyJoystickSafety.Button(device, 0, ref button) && !button, "Unsupported joystick slot is neutral: " + id);
            check(!LegacyJoystickSafety.Analog(device, 0, ref axis) && axis == 0, "Unsupported joystick axis is neutral: " + id);
            check(!LegacyJoystickSafety.Detect(id), "Unsupported device rejected before registration: " + id);
        }
        foreach (var id in new[] { 1, 10 })
        {
            var device = (UnityInputDevice)FormatterServices.GetUninitializedObject(typeof(UnityInputDevice));
            slot.SetValue(device, id);
            check(LegacyJoystickSafety.Detect(id), "Supported device retained: " + id);
            foreach (var index in new[] { -1, 20, int.MaxValue })
            {
                bool button = true; float axis = 1;
                check(!LegacyJoystickSafety.Button(device, index, ref button) && !button, "Invalid button index is neutral: " + id + "/" + index);
                check(!LegacyJoystickSafety.Analog(device, index, ref axis) && axis == 0, "Invalid axis index is neutral: " + id + "/" + index);
            }
            foreach (var index in new[] { 0, 19 })
            {
                bool button = true; float axis = 1;
                check(LegacyJoystickSafety.Button(device, index, ref button) && button, "Supported button delegates to native reader unchanged");
                check(LegacyJoystickSafety.Analog(device, index, ref axis) && axis == 1, "Supported axis delegates to native reader unchanged");
            }
        }

        var broken = new OverflowDevice(); var before = new Pad();

        var caught = false;
        try { Tick(1, before, broken); } catch (IndexOutOfRangeException) { caught = true; }
        check(caught, "Unguarded overflow interrupts real library update");
        caught = false;
        try { Tick(2, before, broken); } catch (InvalidOperationException) { caught = true; }
        check(caught, "Interrupted update reproduces logged pending-tick cascade");

        var pad = new Pad(); var overflow = new OverflowDevice { Guarded = true };

        Tick(1, pad, overflow); pad.Pressed = true; Tick(2, pad, overflow);
        check(pad.Action1.WasPressed, "Guarded overflow allows healthy pad press to commit");
        Tick(3, pad, overflow);
        check(pad.Action1.IsPressed && !pad.Action1.WasPressed, "Next tick remains usable with no pending-tick exception");
        pad.Pressed = false; Tick(4, pad, overflow);
        check(pad.Action1.WasReleased, "Healthy pad release survives overflow device");

    }
}
