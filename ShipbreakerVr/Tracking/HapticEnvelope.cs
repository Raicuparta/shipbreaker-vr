using System;
namespace ShipbreakerVr.Tracking;

// One bounded channel per hand. Short pulses combine by maximum, never by addition.
public sealed class HapticEnvelope
{
    private float pulse, until, nextSend;
    private bool playing;
    public void Queue(float amplitude, float duration, float now)
    {
        if (!Finite(amplitude) || !Finite(duration) || !Finite(now) || amplitude <= 0 || duration <= 0) return;
        if (now >= until) pulse = 0;
        pulse = Math.Max(pulse, Math.Min(1f, amplitude));
        until = Math.Max(until, now + Math.Min(.3f, duration));
        nextSend = Math.Min(nextSend, now);
    }
    public bool Sample(float now, float sustained, out float amplitude)
    {
        amplitude = 0;
        if (!Finite(now) || !Finite(sustained)) { Clear(); return true; }
        if (now >= until) pulse = 0;
        var value = Math.Max(pulse, Math.Max(0, Math.Min(1, sustained)));
        if (value <= 0)
        {
            var stop = playing; playing = false; return stop;
        }
        if (playing && now < nextSend) return false;
        nextSend = now + .05f; playing = true; amplitude = value; return true;
    }
    public void Clear() { pulse = until = nextSend = 0; playing = false; }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
