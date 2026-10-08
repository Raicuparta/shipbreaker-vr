# Changes

## 0.4.44 beta1 — responsive tool switching and heat-bar visibility

- Apply the settling delay only when the same selected tool returns from being stowed for a grab or interaction. Ordinary tool selection no longer adds a minimum visibility delay. Retain synchronized heat-bar hiding and the guard against tools rising from the feet after interactions.
- Hide cutter heat-bar and tool-child UI graphics with the tool mesh; restore native opacity when visible. Includes the couch/gamepad support and shared tool presentation listed under 0.4.42 below. The final 0.4.44 gameplay test was accepted; the release preserves its binary.

## 0.4.43 — cutter heat-bar visibility (test build)

- Hide the cutter's heat-bar UI and attached canvas graphics together with its mesh during grabs, console interactions and the return animation, in both input modes. Restore native UI opacity when the tool is visible; heat/cooldown calculations and interaction input remain unchanged.

## 0.4.42 beta1 — couch/gamepad play and consistent tool presentation

- Add automatic switching between motion controls and native gamepad controls on deliberate button/stick input, plus fixed Gamepad and MotionControllers preferences. Idle tracking does not take over; release controls after switching.
- Support Xbox-style gamepads and Steam Controller gamepad layouts through Steam Input. Couch aiming uses the right stick with independent headset looking, native bindings, menus and gamepad vibration. Individual Steam Input layouts still require testing.
- Let native gamepad arbitration continue after handoff instead of repeatedly overriding the active device, addressing the input loss reported during early couch tests.
- Add readable world-space cutter/grapple aiming rings in couch mode. Preserve native tool range and game rules.
- Use one 65%-of-native default scale for cutter, grapple, held charge and detonator in both modes. Preserve original meshes/textures, native couch placement and calibrated motion aiming. Ignore the retired separate GrappleVisualScale setting.
- Hide stowed tools during grabs and console/door interactions. Wait for cutter/grapple return movement to settle before showing them again; hide separate native shadow copies.
- Retain the established HUD, menus, loading, structural scanner, helmet-damage and XR haptic fixes. Remove temporary interaction tracing; detailed input diagnostics remain off by default.

Versions 0.4.33–0.4.41 were local test builds. The user confirmed working controls, reticles, sizing, doors/consoles and tool return behavior in the final iterations. This release is packaging/diagnostic cleanup of that baseline; see docs/RELEASE-VALIDATION.md for validation limits.

## 0.4.32 beta1 — helmet damage and input recovery prevention

- Attach visor damage to the tracked head and read eye coverage from the actual HDRP/OpenXR render passes.
- Preserve native apparent sizing for both small white sprites and the larger white crack; keep repair/removal under game control.
- Fit the separate critical shattered-glass model across the headset view, retaining its native material and warning lights.
- Retry temporarily unavailable XR eye data instead of permanently reverting to the original close-up effects.
- Guard unsupported legacy joystick slots/indices that can interrupt input updates and leave subsequent updates stuck.
- Keep existing bindings, calibrated tools, HUD settings, scanner rendering and installer behavior unchanged.

Versions 0.4.28–0.4.31 were local test candidates, not public releases.

## 0.4.27 beta — portable release preparation

- Package the accepted 0.4.26 controls, tool calibration, HUD and haptics.
- Disable automatic performance recording on fresh installs; retain F9 and the explicit one-shot option.
- Remove retired performance-comparison switches and the unused early debug-ray setting.
- Add portable install/update/uninstall, Steam library detection, manual folder selection, verified backups, conflict detection and interrupted-operation recovery.
- Preserve user settings while removing known obsolete keys; include clean fresh BepInEx defaults with the console hidden.
- Replace historical setup instructions with current Pico/Frame controls, build instructions, known limitations and a test checklist.

## Accepted gameplay baseline

0.4.25 added native Steam Frame split-gamepad input. 0.4.26 added right full grip as Y and left full grip as D-pad Up while retaining the physical buttons. Earlier work added tracked tools/movement/menus, calibrated visuals, curved HUD, loading/menu fixes, structural scanner room/pressure labels, haptics and cached UI geometry. Detailed engineering history is retained in the source checkout.
