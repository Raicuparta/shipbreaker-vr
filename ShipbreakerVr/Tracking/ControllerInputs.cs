using UnityEngine;

namespace ShipbreakerVr.Tracking;

public readonly struct ControllerInputs
{
    public readonly bool StickAvailable;
    public readonly Vector2 Stick;
    public readonly bool Primary, Secondary, StickClick, Grip;
    public readonly bool FullGamepad, X, Y, Bumper, Menu, View;
    public readonly Vector2 Dpad;
    public ControllerInputs(bool stickAvailable, Vector2 stick, bool primary, bool secondary, bool stickClick, bool grip,
        bool fullGamepad = false, bool x = false, bool y = false, bool bumper = false, bool menu = false, bool view = false, Vector2 dpad = default)
    {
        StickAvailable = stickAvailable;
        Stick = stick;
        Primary = primary;
        Secondary = secondary;
        StickClick = stickClick;
        Grip = grip;
        FullGamepad = fullGamepad; X = x; Y = y; Bumper = bumper; Menu = menu; View = view; Dpad = dpad;
    }

    public bool AnyButton => Primary || Secondary || StickClick || Grip || X || Y || Bumper || Menu || View || Dpad != Vector2.zero;
}

public enum VrButton { None, LeftPrimary, LeftSecondary, LeftStickClick, LeftGrip, RightPrimary, RightSecondary, RightStickClick, RightGrip }

public static class MovementInput
{
    public static bool Button(VrButton button, ControllerInputs left, ControllerInputs right)
    {
        switch (button)
        {
            case VrButton.LeftPrimary: return left.Primary;
            case VrButton.LeftSecondary: return left.Secondary;
            case VrButton.LeftStickClick: return left.StickClick;
            case VrButton.LeftGrip: return left.Grip;
            case VrButton.RightPrimary: return right.Primary;
            case VrButton.RightSecondary: return right.Secondary;
            case VrButton.RightStickClick: return right.StickClick;
            case VrButton.RightGrip: return right.Grip;
            default: return false;
        }
    }

    public static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
        !float.IsInfinity(value.x) && !float.IsInfinity(value.y);

    public static Vector2 Deadzone(Vector2 value, float threshold)
    {
        if (!Finite(value)) return Vector2.zero;
        var magnitude = value.magnitude;
        if (magnitude <= threshold) return Vector2.zero;
        return value / magnitude * Mathf.Clamp01((magnitude - threshold) / (1f - threshold));
    }
}

public sealed class MovementNeutralGate
{
    public bool Armed { get; private set; }
    public bool Sample(bool eligible, Vector2 move, Vector2 turn, bool buttonHeld, float deadzone)
    {
        if (!eligible || !MovementInput.Finite(move) || !MovementInput.Finite(turn)) Armed = false;
        else if (!Armed && move.magnitude <= deadzone && turn.magnitude <= deadzone && !buttonHeld) Armed = true;
        return Armed;
    }
}
