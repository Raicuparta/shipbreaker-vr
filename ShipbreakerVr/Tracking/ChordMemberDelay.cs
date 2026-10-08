namespace ShipbreakerVr.Tracking;

// Briefly reserve a chord member so an almost-simultaneous X+Y does not first
// activate X/defaults or Y/tool wheel. A quick single tap is replayed for one tick.
public sealed class ChordMemberDelay
{
    private bool pending, forwarded;
    private float started;
    public bool Sample(bool eligible, bool held, bool consumed, float now)
    {
        if (!eligible || consumed || float.IsNaN(now) || float.IsInfinity(now))
        { pending = forwarded = false; return false; }
        if (!held)
        {
            var tap = pending && !forwarded;
            pending = forwarded = false;
            return tap;
        }
        if (!pending) { pending = true; started = now; }
        if (now - started >= .12f) forwarded = true;
        return forwarded;
    }
}
