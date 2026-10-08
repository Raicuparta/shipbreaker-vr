namespace ShipbreakerVr.Tracking;

/// <summary>Allows one fallback update per frame only after the host stops updating input.</summary>
public sealed class InputUpdateWatchdog
{
    private int lastExternalFrame;
    private int lastPumpFrame = -1;

    public InputUpdateWatchdog(int frame) => lastExternalFrame = frame;
    public void ObserveExternalUpdate(int frame) => lastExternalFrame = frame;

    public bool TryPump(int frame)
    {
        if (frame - lastExternalFrame < 2 || lastPumpFrame == frame) return false;
        lastPumpFrame = frame;
        return true;
    }
}
