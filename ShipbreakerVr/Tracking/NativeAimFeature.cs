namespace ShipbreakerVr.Tracking;

public enum NativeAimFeatureKind { None, Position, Rotation, IsTracked, TrackingState }

public static class NativeAimFeature
{
    public static bool CanUseSharedValidity(bool pointerPosition, bool pointerRotation,
        bool pointerTracked, bool pointerState, bool deviceTracked, bool deviceState) =>
        pointerPosition && pointerRotation && !pointerTracked && !pointerState && deviceTracked && deviceState;

    // OpenXR exposes the pointer action as pointer/position etc., with PointerPosition
    // aliases in XR SDK layouts. Normalize only spelling; do not reinterpret grip/head features.
    public static NativeAimFeatureKind Classify(string name)
    {
        if (name == null) return NativeAimFeatureKind.None;
        switch (name.Replace("/", "").Replace(" ", "").Replace("_", "").ToLowerInvariant())
        {
            case "pointerposition": return NativeAimFeatureKind.Position;
            case "pointerrotation": return NativeAimFeatureKind.Rotation;
            case "pointeristracked": return NativeAimFeatureKind.IsTracked;
            case "pointertrackingstate": return NativeAimFeatureKind.TrackingState;
            default: return NativeAimFeatureKind.None;
        }
    }
}
