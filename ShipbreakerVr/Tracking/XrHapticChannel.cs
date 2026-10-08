using UnityEngine;
using UnityEngine.XR;

namespace ShipbreakerVr.Tracking;

// Standard XR device capabilities, independent of controller manufacturer/layout.
internal sealed class XrHapticChannel
{
    private readonly XRNode node;
    private InputDevice output;
    private bool playing, reported;
    internal XrHapticChannel(XRNode node) => this.node = node;
    internal void Send(float amplitude)
    {
        if (amplitude <= 0) { Stop(); return; }
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!output.Equals(device)) { Stop(); output = device; reported = false; }
        if (!device.isValid) return;
        if (!device.TryGetHapticCapabilities(out var caps) || !caps.supportsImpulse || caps.numChannels == 0)
        {
            if (!reported) { reported = true; Debug.Log($"[ShipbreakerVr] Haptics {node}: {device.name} did not report impulse support."); }
            return;
        }
        var sent = device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), .07f);
        playing |= sent;
        if (!reported) { reported = true; Debug.Log($"[ShipbreakerVr] Haptics {node}: device={device.name}; impulse accepted={sent}"); }
    }
    internal void Stop()
    {
        try { if (playing && output.isValid) output.StopHaptics(); }
        catch (System.Exception) { /* Runtime can disappear during disconnect. */ }
        finally { playing = false; }
    }
}
