using System.Collections.Generic;
using UnityEngine;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// Transparent sorting compares sorting layer/order before material queue/depth.
// Put converted UI above ordinary world renderers while preserving native UI order.
internal sealed class VrUiSorting
{
    private sealed class Entry
    {
        internal Canvas Canvas;
        internal int Layer, Value, Order, DesiredOrder;
        internal bool Loading;
    }
    private static readonly List<Entry> all = new List<Entry>();
    private static bool orderDirty = true;
    private static int checkedFrame = -1, topLayer;
    private readonly List<Entry> owned = new List<Entry>();
    private readonly HashSet<Canvas> known = new HashSet<Canvas>();
    private readonly List<Canvas> canvases = new List<Canvas>();
    internal void Apply(Canvas root)
    {
        canvases.Clear(); root.GetComponentsInChildren(true, canvases);
        foreach (var canvas in canvases)
        {
            if (!canvas || (canvas != root && !canvas.overrideSorting)) continue;
            if (known.Contains(canvas)) continue;
            var entry = new Entry { Canvas = canvas, Layer = canvas.sortingLayerID,
                Value = SortingLayer.GetLayerValueFromID(canvas.sortingLayerID), Order = canvas.sortingOrder,
                Loading = root.name == "LoadScreenCanvas" };
            owned.Add(entry); all.Add(entry); known.Add(canvas); orderDirty = true;
            Debug.Log($"[ShipbreakerVr] UI sorting registration: {canvas.name}; native layer={SortingLayer.IDToName(entry.Layer)}/{entry.Value}; order={entry.Order}");
        }
        if (checkedFrame != Time.frameCount)
        {
            checkedFrame = Time.frameCount;
            if (all.RemoveAll(entry => !entry.Canvas) > 0) orderDirty = true;
            var nextLayer = 0; var topValue = int.MinValue;
            foreach (var layer in SortingLayer.layers)
                if (layer.value > topValue) { topValue = layer.value; nextLayer = layer.id; }
            if (nextLayer != topLayer) { topLayer = nextLayer; orderDirty = true; }
        }
        if (orderDirty)
        {
            all.Sort((a, b) => a.Loading != b.Loading ? a.Loading.CompareTo(b.Loading) : PresentationGeometry.CompareUiOrder(a.Value, a.Order, b.Value, b.Order));
            var rank = 0;
            for (var i = 0; i < all.Count; i++)
            {
                var entry = all[i];
                if (i > 0 && entry.Loading != all[i - 1].Loading) rank = 0;
                else if (i > 0 && (entry.Value != all[i - 1].Value || entry.Order != all[i - 1].Order)) rank++;
                entry.DesiredOrder = PresentationGeometry.UiSortOrder(entry.Loading, rank);
                Enforce(entry);
            }
            orderDirty = false;
            VrUiPerformance.SortRebuilt();
        }
        // Still discover canvases and reassert this owner's order every callback:
        // native animation can change values even when the registry is unchanged.
        foreach (var entry in owned) Enforce(entry);
    }
    private static void Enforce(Entry entry)
    {
        if (!entry.Canvas) return;
        if (entry.Canvas.sortingLayerID != topLayer) entry.Canvas.sortingLayerID = topLayer;
        if (entry.Canvas.sortingOrder != entry.DesiredOrder) entry.Canvas.sortingOrder = entry.DesiredOrder;
    }
    internal void Restore()
    {
        foreach (var entry in owned)
        {
            if (entry.Canvas) { entry.Canvas.sortingLayerID = entry.Layer; entry.Canvas.sortingOrder = entry.Order; }
            all.Remove(entry);
        }
        if (owned.Count > 0) orderDirty = true;
        owned.Clear(); known.Clear(); canvases.Clear();
    }
}
