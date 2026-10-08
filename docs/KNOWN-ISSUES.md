# Beta status and final smoke test

## Accepted baseline and limits

The user accepted the final 0.4.44 gameplay test after heat-bar visibility and ordinary tool-switch delay corrections. Earlier tests confirmed gamepad responsiveness, aiming reticles, automatic switching, shared 65% tool scaling and door/console interactions. Temporary interaction tracing is removed and verbose input logging is opt-in. This is not a recorded pass of every regression on every controller.

The motion-control baseline has been exercised on Pico Neo 3 over Steam Link and Steam Frame through SteamVR. Couch play was exercised with a separate gamepad. Other headsets/runtimes, mixed controller pairs, left-handed tools, Steam Controller hardware and arbitrary Steam Input/game binding layouts are unverified. Steam Controller support requires a gamepad layout; keyboard/mouse-only layouts do not activate automatic gamepad mode.

Release buttons/triggers and center sticks after switching input modes, reconnecting or returning from dashboard/focus loss. Motion tracking alone does not switch modes. For a fixed input source, use the Input.Mode preference documented in CONTROLS.md. Native tasks/View is context dependent.

Menus use gamepad navigation, not a controller laser mouse. Menu hands/controller meshes are intentionally absent. The head-following HUD is not the aiming reference in couch mode; use the world-space tool ring and right stick. A white ring means a surface hit, not necessarily a valid tool target.

UI, loading composition, structural room/pressure markers, tool alignment and helmet damage were iterated in headset. Unseen screens/ships may still need adjustments. A report about the starting scanner's stereo appearance on Quest 3/VDXR remains unconfirmed; this update does not change scanner rendering. The critical helmet glass retains its native material and lights; coverage on other headsets remains unverified.

The mod depends on internal Mono/Harmony interfaces and pinned Unity/OpenXR packages. Future game updates can break hooks. The scanner and haptic patches log warnings if optional hooks fail. No engine/package upgrade is included here.

UI caching reduced measured mod work, and smoothness on High was reported during development. This is not a promise of native headset refresh or a particular frame rate. CPU/GPU, scene, resolution, reprojection and streaming all matter. Automatic captures are off; F9 is available for diagnosis. The installer does not change SteamVR or game graphics settings.


## Portable-package smoke test

After installing the extracted ZIP, check:

1. Log identifies ShipbreakerVr 0.4.44; VR starts and controls respond after releasing/centering.
2. Switch between gamepad and tracked controls; verify idle controllers/head movement do not take over. Check reconnect, focus loss and handoff during a tool action.
3. In gamepad mode, move/turn with sticks, look independently with the headset, and check cutter/grapple rings at near/far targets.
4. Check tool wheel, scanner, flashlight, tethers, Menu/View, options tabs and each controller layout's documented shortcuts.
5. Check Stinger first shot, Splitsaw angle, grapple/retract/push/tethers and charge/detonator. Verify sizes and muzzle alignment in both modes.
6. Switch tools without a mod-added delay. Hold/release RB, use a door and console, and confirm tools and cutter heat bar hide/return together while interaction remains functional.
7. Check main menu, habitat computer/back navigation, loading text over static, Free Play card clipping and HUD readability.
8. Check structural room/pressure labels, haptics, white/critical helmet damage and repair; quit/relaunch and inspect for repeated errors.

Automated build/installer checks do not replace a headset run of the final ZIP. See RELEASE-VALIDATION.md in the source repository for the exact package checks. No new performance comparison is required merely to validate packaging.
