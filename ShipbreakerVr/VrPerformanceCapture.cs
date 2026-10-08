using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BBI.Unity.Game;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.XR;

namespace ShipbreakerVr;

// Opt-in baseline measurement: no rendering, input or quality settings are changed.
internal sealed class VrPerformanceCapture
{
    private readonly ConfigEntry<bool> captureNextWorkyard;
    private float gameplaySince = -1f;

    internal VrPerformanceCapture(ConfigFile config)
    {
        captureNextWorkyard = config.Bind("Performance", "CaptureNextWorkyard", false,
            "One-shot diagnostic: capture 20 seconds after ten seconds of uninterrupted VR gameplay. Resets false when started. F9 also captures manually.");
        Debug.Log("[ShipbreakerVr] Performance recorder ready; automatic capture armed=" + captureNextWorkyard.Value);
    }
    private readonly List<double> intervals = new List<double>(8192);
    private readonly List<double> cpu = new List<double>(8192);
    private readonly List<double> gpu = new List<double>(8192);
    private readonly List<double> xrGpu = new List<double>(8192);
    private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
    private readonly FrameTiming[] timing = new FrameTiming[1];
    private readonly StringBuilder report = new StringBuilder(4096);
    private XRDisplaySubsystem display;
    private float requestedAt, startedAt;
    private bool capturing, measuring, frameTimingAvailable, xrTimingAvailable;
    private ulong lastTiming;
    private int gcStart, dropsStart, stateChanges, unfocusedFrames;
    private GameSession.GameState previousState;

    internal void Tick()
    {
        try
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (capturing) Finish("F9 stop"); else Begin();
                return;
            }
            if (!capturing)
            {
                if (captureNextWorkyard.Value && ModXrManager.IsVrEnabled &&
                    GameSession.CurrentGameState == GameSession.GameState.Gameplay)
                {
                    if (gameplaySince < 0f) gameplaySince = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - gameplaySince >= 10f) Begin();
                }
                else gameplaySince = -1f;
                return;
            }
            if (!ModXrManager.IsVrEnabled) { Finish("VR disabled"); return; }
            if (!measuring)
            {
                // Exclude startup logging, allocation/JIT work and the keypress frame.
                if (Time.realtimeSinceStartup - requestedAt < 2f) return;
                measuring = true;
                VrUiPerformance.Reset();
                startedAt = Time.realtimeSinceStartup;
                gcStart = GC.CollectionCount(0);
                dropsStart = DroppedFrames();
                previousState = GameSession.CurrentGameState;
                return;
            }
            Add(intervals, Time.unscaledDeltaTime * 1000.0);
            if (!Application.isFocused) unfocusedFrames++;
            if (GameSession.CurrentGameState != previousState)
            {
                stateChanges++;
                previousState = GameSession.CurrentGameState;
            }
            SampleOptionalTimings();
            if (Time.realtimeSinceStartup - startedAt >= 20f) Finish("20 seconds complete");
        }
        catch (Exception error)
        {
            capturing = false;
            VrUiPerformance.Enabled = false;
            Debug.LogWarning("[ShipbreakerVr] Performance capture stopped: " + error.Message);
        }
    }

    private void Begin()
    {
        if (!ModXrManager.IsVrEnabled)
        {
            Debug.Log("[ShipbreakerVr] Enable VR before starting F9 performance capture.");
            return;
        }
        intervals.Clear(); cpu.Clear(); gpu.Clear(); xrGpu.Clear(); report.Clear();
        captureNextWorkyard.Value = false;
        VrUiPerformance.Enabled = true;
        measuring = false; lastTiming = 0; stateChanges = 0; unfocusedFrames = 0;
        frameTimingAvailable = true; xrTimingAvailable = true;
        displays.Clear(); SubsystemManager.GetInstances(displays);
        display = displays.Find(item => item.running);
        var refresh = 0f;
        try { if (display != null) display.TryGetDisplayRefreshRate(out refresh); } catch { }
        report.AppendLine($"ShipbreakerVr 0.4.27 performance capture; UTC={DateTime.UtcNow:o}");
        report.AppendLine($"CPU={SystemInfo.processorType}; GPU={SystemInfo.graphicsDeviceName}; api={SystemInfo.graphicsDeviceType}; state={GameSession.CurrentGameState}");
        report.AppendLine($"quality={QualitySettings.GetQualityLevel()}; eyeTexture={XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}; eyeScale={XRSettings.eyeTextureResolutionScale}; refreshHz={refresh} (0=unavailable); stereo={XRSettings.stereoRenderingMode}; vSync={QualitySettings.vSyncCount}; targetFPS={Application.targetFrameRate}");
        foreach (var camera in Camera.allCameras)
            report.AppendLine($"camera={camera.name}; stereo={camera.stereoEnabled}; texture={(camera.targetTexture ? camera.targetTexture.width + "x" + camera.targetTexture.height : "display")}; mask={camera.cullingMask}");
        // Exercise statistics formatting before the measured interval.
        Stats(intervals);
        Debug.Log("[ShipbreakerVr] PERF BEGIN: two-second warmup, then 20-second capture. Keep playing normally.\n" + report);
        requestedAt = Time.realtimeSinceStartup;
        capturing = true;
    }

    private void SampleOptionalTimings()
    {
        if (frameTimingAvailable)
        {
            try
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].cpuTimeFrameComplete != lastTiming)
                {
                    lastTiming = timing[0].cpuTimeFrameComplete;
                    Add(cpu, timing[0].cpuFrameTime);
                    Add(gpu, timing[0].gpuFrameTime);
                }
            }
            catch { frameTimingAvailable = false; }
        }
        if (xrTimingAvailable)
        {
            try
            {
                if (display != null && display.running && display.TryGetAppGPUTimeLastFrame(out var seconds))
                    Add(xrGpu, seconds * 1000.0);
            }
            catch { xrTimingAvailable = false; }
        }
    }

    private int DroppedFrames()
    {
        try { if (display != null && display.running && display.TryGetDroppedFrameCount(out var count)) return count; }
        catch { }
        return -1;
    }

    private static void Add(List<double> values, double value)
    {
        if (value > 0 && !double.IsNaN(value) && !double.IsInfinity(value)) values.Add(value);
    }

    private static string Stats(List<double> values)
    {
        if (values.Count == 0) return "unavailable (not zero cost)";
        values.Sort();
        double total = 0;
        foreach (var value in values) total += value;
        double Percentile(double fraction) => values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * fraction) - 1)];
        return $"n={values.Count}, avg={total / values.Count:F2}, p95={Percentile(.95):F2}, p99={Percentile(.99):F2}, max={values[values.Count - 1]:F2} ms";
    }

    private void Finish(string reason)
    {
        capturing = false;
        VrUiPerformance.Enabled = false;
        if (!measuring) { Debug.Log("[ShipbreakerVr] PERF cancelled before measurement started."); return; }
        var elapsed = Time.realtimeSinceStartup - startedAt;
        var collections = GC.CollectionCount(0) - gcStart;
        var dropsEnd = DroppedFrames();
        var dropped = dropsStart >= 0 && dropsEnd >= dropsStart ? (dropsEnd - dropsStart).ToString() : "unavailable";
        report.AppendLine($"PERF END: {reason}; elapsed={elapsed:F1}s; gameStateChanges={stateChanges}; unfocusedFrames={unfocusedFrames}; GC0={collections}; dropped={dropped}");
        report.AppendLine("Frame intervals: " + Stats(intervals));
        report.AppendLine("CPU frame (includes waits, not CPU busy time): " + Stats(cpu));
        report.AppendLine("GPU frame: " + Stats(gpu));
        report.AppendLine("XR app GPU (runtime-reported samples): " + Stats(xrGpu));
        report.AppendLine(VrUiPerformance.Report(intervals.Count));
        report.AppendLine($"Ending quality={QualitySettings.GetQualityLevel()}; ending eyeTexture={XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}");
        report.AppendLine("Streaming/network latency is not measured. Unsupported metrics are unavailable. No settings changed.");
        var text = report.ToString();
        Debug.Log("[ShipbreakerVr] " + text);
        try
        {
            var path = Path.Combine(Paths.BepInExRootPath, "ShipbreakerVr-performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
            File.WriteAllText(path, text);
            Debug.Log("[ShipbreakerVr] Performance report saved: " + path);
        }
        catch (Exception error) { Debug.LogWarning("[ShipbreakerVr] Could not save report; summary is in LogOutput.log: " + error.Message); }
    }

    internal void Stop() { if (capturing) Finish("plugin disabled"); }
}
