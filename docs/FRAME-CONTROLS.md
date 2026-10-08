# Frame controls

The current bindings and full-grip shortcuts are documented in [CONTROLS.md](CONTROLS.md).

Frame controls were reported working on hardware in 0.4.25 and the full-grip shortcuts in 0.4.26. Version 0.4.27 preserves those routes. This is Windows PCVR through SteamVR, not a standalone port.

The native profile uses `XR_VALVE_frame_controller_interaction` and `/interaction_profiles/valve/frame_controller_valve`, registered before OpenXR initialization. Runtime support supplies independent ABXY, D-pad, bumpers, Menu/View, squeeze click, trigger and pose/haptic features. Both controllers must expose the full layout. Unsupported runtimes retain standard profiles; a fallback profile cannot guarantee all Frame buttons.

For diagnostics, expect `Frame native actions bound` and `Button layout: native split gamepad` in the game log. [Valve input documentation](https://partner.steamgames.com/doc/steamhardware/steamframe/input) describes the native profile. Historical candidate notes are archived under `history` in the source checkout.
