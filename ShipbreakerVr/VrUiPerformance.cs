using System;
using System.Diagnostics;
using System.Text;

namespace ShipbreakerVr;

// Main-thread elapsed time for mod UI work only; not GPU time or whole-game CPU time.
internal static class VrUiPerformance
{
    internal enum Work { Placement, Sorting, Materials, Clipping, RoomMarkers, Curvature, MeshSubmission, MeshRestore }
    private static readonly long[] ticks = new long[8], calls = new long[8];
    internal static bool Enabled;
    private static long cacheHits, curveRebuilds, sortRebuilds;
    internal static void CurveCacheHit() { if (Enabled) cacheHits++; }
    internal static void CurveRebuilt() { if (Enabled) curveRebuilds++; }
    internal static void SortRebuilt() { if (Enabled) sortRebuilds++; }

    internal readonly struct Scope : IDisposable
    {
        private readonly bool enabled;
        private readonly Work work;
        private readonly long start;
        internal Scope(Work work)
        {
            this.work = work;
            enabled = Enabled;
            start = enabled ? Stopwatch.GetTimestamp() : 0;
        }
        public void Dispose()
        {
            if (!enabled) return;
            ticks[(int)work] += Stopwatch.GetTimestamp() - start;
            calls[(int)work]++;
        }
    }

    internal static void Reset()
    {
        Array.Clear(ticks, 0, ticks.Length);
        Array.Clear(calls, 0, calls.Length);
        cacheHits = curveRebuilds = sortRebuilds = 0;
    }

    internal static string Report(int frames)
    {
        var result = new StringBuilder("Mod UI elapsed timings (inclusive; nested stages must not be summed):\n");
        result.AppendLine($"  Curved mesh reuse={cacheHits}; rebuilt={curveRebuilds}; global sorting rebuilds={sortRebuilds}");
        for (var i = 0; i < ticks.Length; i++)
        {
            var ms = ticks[i] * 1000.0 / Stopwatch.Frequency;
            result.AppendLine($"  {(Work)i}: calls={calls[i]}, total={ms:F2} ms, per sampled frame={(frames > 0 ? ms / frames : 0):F3} ms");
        }
        return result.ToString();
    }
}
