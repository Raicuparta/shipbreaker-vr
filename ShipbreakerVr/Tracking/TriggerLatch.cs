namespace ShipbreakerVr.Tracking;

// Require a deliberate release after entering a context or recovering tracking.
public sealed class TriggerLatch
{
    private bool armed;
    public bool Held { get; private set; }
    public bool Pressed { get; private set; }
    public bool Released { get; private set; }

    public void Sample(bool eligible, float value)
    {
        Pressed = Released = false;
        if (!eligible || float.IsNaN(value) || float.IsInfinity(value))
        {
            armed = Held = false;
            return;
        }
        if (value <= 0.25f) { Released = Held; armed = true; Held = false; return; }
        if (armed && !Held && value >= 0.75f) { Held = true; Pressed = true; }
    }
}
