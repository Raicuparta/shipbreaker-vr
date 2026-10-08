using System;

namespace ShipbreakerVr.Tracking;

// Presentation only: native equip/input/animation state must never depend on this gate.
internal sealed class ToolReturnVisibility
{
    private float elapsed, stable;
    private bool visible, returningFromStow;
    internal bool Sample(bool selected, bool held, bool poseSettled, float deltaTime)
    {
        // Selecting a different tool is not a return from a grab/interaction.
        // Only wait when the same selected tool was actually put away.
        if (!selected) { elapsed = stable = 0; visible = returningFromStow = false; return false; }
        if (!held) { elapsed = stable = 0; visible = false; returningFromStow = true; return false; }
        if (!returningFromStow) { visible = true; return true; }
        if (visible) return true; // Normal tool shake must not flicker visibility.
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return false;
        // A hitch is not evidence that the intervening animation was stationary.
        var dt = Math.Min(deltaTime, .05f);
        elapsed += dt;
        stable = poseSettled ? stable + dt : 0;
        visible = elapsed >= .25f && stable >= .12f;
        return visible;
    }
}
