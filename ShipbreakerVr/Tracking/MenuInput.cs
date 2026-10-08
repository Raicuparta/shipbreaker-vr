using UnityEngine;

namespace ShipbreakerVr.Tracking;

// Pure routing policy. No weapon trigger is forwarded to the game's virtual pad.
public struct MenuInput
{
    public Vector2 Navigate, Scroll, Dpad;
    public bool LeftTrigger, RightTrigger;
    public bool Confirm, Back, Interact, Tools, Mode, Start, Select, PreviousTab, NextTab, Misc1, Misc2;

    // Split controllers with a complete console layout use the game's button positions.
    // Tool triggers and movement remain in the tracked gameplay adapters.
    public static MenuInput Gamepad(bool gameplay, bool wheelOpen, ControllerInputs left, ControllerInputs right,
        float deadzone, bool leftTrigger, bool rightTrigger)
    {
        if (right.Menu) return new MenuInput { Start = true };
        if (left.View) return new MenuInput { Select = true };
        // Grip is OpenXR squeeze/click (the full-press stage), not squeeze/value.
        // Keep the face/D-pad bindings; either input can hold the same action.
        var y = right.Y || right.Grip;
        var dpad = new Vector2(left.Dpad.x, left.Grip ? 1f : left.Dpad.y);
        var tools = gameplay && y;
        return new MenuInput
        {
            Navigate = !gameplay || wheelOpen || tools ? MovementInput.Deadzone(left.Stick, deadzone) : Vector2.zero,
            Scroll = gameplay ? Vector2.zero : MovementInput.Deadzone(right.Stick, deadzone),
            Dpad = wheelOpen || tools ? Vector2.zero : dpad,
            Confirm = !gameplay && right.Primary, Back = !gameplay && right.Secondary,
            Interact = gameplay && !wheelOpen && !tools && right.X, Tools = tools,
            Misc1 = !gameplay && y, Misc2 = !gameplay && right.X,
            PreviousTab = !wheelOpen && !tools && left.Bumper, NextTab = !wheelOpen && !tools && right.Bumper,
            LeftTrigger = !gameplay && leftTrigger, RightTrigger = !gameplay && rightTrigger
        };
    }

    public static MenuInput Route(bool gameplay, bool wheelOpen, Vector2 left, Vector2 right,
        bool confirm, bool back, bool interact, bool tools, bool mode, bool start,
        bool previousTab, bool nextTab, bool misc1 = false, bool misc2 = false, bool select = false, bool dpadMode = false, bool leftTrigger = false, bool rightTrigger = false)
    {
        // A pause chord must not also confirm, go back or select a tool.
        if (start) return new MenuInput { Start = true };
        if (select) return new MenuInput { Select = true };
        return new MenuInput
        {
            LeftTrigger = !gameplay && leftTrigger,
            RightTrigger = !gameplay && rightTrigger,
            Navigate = !gameplay ? left : wheelOpen || tools ? right : Vector2.zero,
            Dpad = gameplay && dpadMode && !wheelOpen && !tools ? DpadInput.Direction(left) : Vector2.zero,
            Scroll = gameplay ? Vector2.zero : right,
            Confirm = !gameplay && confirm,
            Back = !gameplay && back,
            Interact = gameplay && !wheelOpen && !tools && interact,
            Tools = gameplay && tools,
            Mode = gameplay && !wheelOpen && !tools && !dpadMode && mode,
            Start = start,
            PreviousTab = !gameplay && previousTab,
            NextTab = !gameplay && nextTab,
            Misc1 = !gameplay && misc1,
            Misc2 = !gameplay && misc2
        };
    }
}

public static class DpadInput
{
    public static Vector2 Direction(Vector2 stick)
    {
        if (!MovementInput.Finite(stick) || stick.magnitude < .5f) return Vector2.zero;
        return Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
            ? new Vector2(Mathf.Sign(stick.x), 0f) : new Vector2(0f, Mathf.Sign(stick.y));
    }
}

public sealed class ShiftedStickGate
{
    private bool waitingForCenter;
    public Vector2 ForMovement(bool shifted, Vector2 stick, float deadzone)
    {
        if (shifted || !MovementInput.Finite(stick)) waitingForCenter = true;
        else if (stick.magnitude <= deadzone) waitingForCenter = false;
        return waitingForCenter ? Vector2.zero : stick;
    }
}

public sealed class ButtonChord
{
    private bool armed;
    public bool Held { get; private set; }
    public bool ConsumesButtons { get; private set; }

    public void Sample(bool eligible, bool first, bool second)
    {
        if (!eligible) { armed = Held = ConsumesButtons = false; return; }
        if (!first && !second) { armed = true; Held = ConsumesButtons = false; return; }
        if (armed && first && second) { ConsumesButtons = Held = true; armed = false; }
        else Held = Held && first && second;
    }

    public static bool ValidBinding(VrButton first, VrButton second) =>
        first != VrButton.None && second != VrButton.None && first != second;
}

public sealed class MenuNeutralGate
{
    private readonly MovementNeutralGate gate = new MovementNeutralGate();
    private int? previousContext;

    public bool Sample(int context, bool eligible, Vector2 left, Vector2 right, bool held, float deadzone)
    {
        if (previousContext != context) gate.Sample(false, left, right, held, deadzone);
        previousContext = context;
        return gate.Sample(eligible, left, right, held, deadzone);
    }
}
