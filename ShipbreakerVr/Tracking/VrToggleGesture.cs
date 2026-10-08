using UnityEngine;

namespace ShipbreakerVr.Tracking;

public sealed class VrToggleGesture
{
    private bool armed, fired, timing;
    private float started;
    public bool ConsumesButtons { get; private set; }
    public bool Sample(bool eligible, ControllerInputs left, ControllerInputs right, float now)
    {
        if (!eligible || !left.StickAvailable || !right.StickAvailable || !MovementInput.Finite(left.Stick) ||
            !MovementInput.Finite(right.Stick) || float.IsNaN(now) || float.IsInfinity(now))
        { armed = fired = timing = ConsumesButtons = false; return false; }
        var released = !left.Grip && !right.Grip && !left.StickClick && !right.StickClick;
        if (released) { armed = true; fired = timing = ConsumesButtons = false; return false; }
        if (!armed) return false;
        // Start with both stick clicks before squeezing grips; consume as the chord forms.
        ConsumesButtons |= left.StickClick && right.StickClick && (left.Grip || right.Grip);
        var full = left.Grip && right.Grip && left.StickClick && right.StickClick &&
            left.Stick.magnitude <= .25f && right.Stick.magnitude <= .25f;
        if (!full || fired) { timing = false; return false; }
        if (!timing || now < started) { timing = true; started = now; return false; }
        if (now - started < 2f) return false;
        fired = true;
        return true;
    }
}
