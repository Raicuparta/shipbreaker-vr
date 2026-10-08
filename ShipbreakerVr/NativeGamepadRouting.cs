using InControl;

namespace ShipbreakerVr;

internal static class NativeGamepadRouting
{
    // Only the handoff neutral gate may suppress native input. Never retain a
    // chosen pad here: InControl must be able to select a new active device as
    // Steam Input/XInput slots change, including activity below our mode threshold.
    internal static InputDevice Filter(InputDevice native, bool couch, bool ready) =>
        couch && !ready ? InputDevice.Null : native;
}
