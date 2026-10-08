using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShipbreakerVr.Tracking;

/// <summary>Supports a legacy-input game that does not drive the imported Input System player loop.</summary>
public sealed class InputSystemUpdateBridge : IDisposable
{
    private readonly InputUpdateWatchdog watchdog = new InputUpdateWatchdog(Time.frameCount);
    private bool pumping;
    private bool reported;
    public int PumpCount { get; private set; }
    public int HostUpdateCount { get; private set; }

    public InputSystemUpdateBridge() => InputSystem.onAfterUpdate += AfterUpdate;

    private void AfterUpdate()
    {
        if (pumping || InputState.currentUpdateType == InputUpdateType.None ||
            InputState.currentUpdateType == InputUpdateType.BeforeRender) return;
        watchdog.ObserveExternalUpdate(Time.frameCount);
        HostUpdateCount++;
    }

    public void UpdateIfNeeded()
    {
        if (!watchdog.TryPump(Time.frameCount)) return;
        if (!reported)
        {
            reported = true;
            Debug.Log("[ShipbreakerVr] Host is not updating the imported Input System; enabling a once-per-frame fallback while controller diagnostics are active.");
        }
        pumping = true;
        try { InputSystem.Update(); PumpCount++; }
        finally { pumping = false; }
    }

    public void Dispose() => InputSystem.onAfterUpdate -= AfterUpdate;
}
