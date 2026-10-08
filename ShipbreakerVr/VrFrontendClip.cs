using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// A screen-space canvas is implicitly clipped by the display viewport. Restore
// that boundary for the world-space frontend without adding game/ECS components.
internal sealed class VrFrontendClip
{
    private readonly List<MaskableGraphic> graphics = new List<MaskableGraphic>();
    private readonly List<RectMask2D> masks = new List<RectMask2D>();
    private readonly HashSet<MaskableGraphic> touched = new HashSet<MaskableGraphic>();
    private bool logged;

    internal void Apply(Canvas canvas)
    {
        if (canvas.name != "Canvas - FrontEnd" || !(canvas.transform is RectTransform root)) return;
        graphics.Clear(); canvas.GetComponentsInChildren(false, graphics);
        foreach (var graphic in graphics)
        {
            if (!graphic || graphic.canvas.rootCanvas != canvas) continue;
            var clip = root.rect;
            if (NativeClip(graphic, out var native, out var valid))
                clip = valid ? PresentationGeometry.IntersectClip(clip, native) : Rect.zero;
            // A disjoint clip must remain enabled; disabling it would reveal the list.
            graphic.SetClipRect(clip, true);
            touched.Add(graphic);
        }
        if (!logged && graphics.Count > 0)
        { logged = true; Debug.Log($"[ShipbreakerVr] Frontend viewport clipping active: graphics={graphics.Count}; rect={root.rect}"); }
    }

    private bool NativeClip(MaskableGraphic graphic, out Rect rect, out bool valid)
    {
        rect = default; valid = false;
        var parent = graphic.maskable ? MaskUtilities.GetRectMaskForClippable(graphic) : null;
        if (!parent) return false;
        masks.Clear(); MaskUtilities.GetRectMasksForClip(parent, masks);
        rect = Clipping.FindCullAndClipWorldRect(masks, out valid);
        return true;
    }

    internal void Restore()
    {
        foreach (var graphic in touched)
        {
            if (!graphic) continue;
            NativeClip(graphic, out var rect, out var valid);
            graphic.SetClipRect(rect, valid);
            graphic.RecalculateClipping();
        }
        touched.Clear(); graphics.Clear(); masks.Clear();
    }
}
