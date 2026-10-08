using System;

namespace ShipbreakerVr.Tracking;

public enum VrInputMode { Auto, MotionControllers, Gamepad }

// Only fresh button/axis activations count. Held controls, analog jitter, poses,
// reconnects and focus restoration must not repeatedly steal input ownership.
public sealed class InputActivity
{
    private readonly bool[] held, seen;
    public InputActivity(int channels) { held = new bool[channels]; seen = new bool[channels]; }
    public bool Observe(int channel, float value, bool available = true)
    {
        if (!available || float.IsNaN(value) || float.IsInfinity(value))
        { seen[channel] = held[channel] = false; return false; }
        var active = Math.Abs(value) >= (held[channel] ? .2f : .55f);
        var pressed = seen[channel] && active && !held[channel];
        held[channel] = active; seen[channel] = true;
        return pressed;
    }
    public void Reset() { Array.Clear(held, 0, held.Length); Array.Clear(seen, 0, seen.Length); }
}

public sealed class InputModeSelection
{
    public bool Motion { get; private set; } = true;
    private float lastMotion = float.NegativeInfinity;
    public bool Sample(VrInputMode setting, bool motionAvailable, bool padAvailable,
        bool motionPressed, bool padPressed, float now)
    {
        var before = Motion;
        if (motionPressed && motionAvailable) lastMotion = now;
        if (setting != VrInputMode.Auto) Motion = setting == VrInputMode.MotionControllers;
        else if (motionPressed && motionAvailable) Motion = true;
        // Steam Input can expose an echo of a tracked controller as a gamepad.
        // Simultaneous/near-simultaneous tracked activity takes precedence.
        else if (padPressed && padAvailable && now - lastMotion > .3f) Motion = false;
        return before != Motion;
    }
}
