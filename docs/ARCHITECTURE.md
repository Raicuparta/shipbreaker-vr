# Shipbreaker VR architecture

Based on Raicuparta/shipbreaker-vr commit `0c13a9997b9725d699092688d51277f4bc9b0211`. Current release: **0.4.44 beta1**. Earlier sections describe their original milestone and are retained as historical design context.

## 0.4.42 input ownership and presentation

`VrInputMode` samples native InControl devices and tracked controller activity before PlayerActions consume the tick. `Tracking/InputModeSelection` ignores poses, first-seen held controls and drift, and prioritizes near-simultaneous motion input over gamepad echoes. Each handoff cancels owned tool/menu actions and requires neutral controls. `NativeGamepadRouting` suppresses native input only until that neutral gate is armed; normal InControl device selection then resumes.

Couch mode releases the synthetic motion pad's action-set ownership. Tool adapters fall back to native inputs/aim; headset look remains independent of body/right-stick aim. XR haptics stop for idle tracked controllers while native gamepad vibration remains game-owned. Fixed input preferences are available.

`VrCouchReticle` renders a raycast endpoint as a world-space UI ring with no raycaster/input component. Its angular size and clipping adjustments are presentation only. `VrAvatarVisuals` preserves native couch placement, shares ToolVisualScale across all held tools, and hides unequipped models based on equipment and hand state. `ToolReturnVisibility` waits for native root motion to settle before revealing a returning cutter/grapple, then latches visibility through normal shake. All pose/visibility overrides restore before native gameplay updates. `VrHandPropVisuals` scales only copied held charge/detonator geometry; native bones, deployed charges and surface previews stay game-owned.

Temporary interaction tracing is removed from release builds. Detailed motion/gamepad input logging is opt-in under Diagnostics.VerboseMenuInput. HUD, scanner, loading and helmet presentation follow the established 0.4.32 baseline.

In 0.4.44 the return gate distinguishes a selected-but-stowed tool from an unselected tool: ordinary selection is immediate; returning from a grab or interaction waits for native motion to settle. The cutter heat bar's explicitly owned UI graphics and tool-child canvases share the mesh visibility gate. CanvasRenderer opacity is restored with the other frame overrides, leaving native heat updates and interaction behavior intact.

## 0.4.10 startup boundary

HUD mesh hooks resolve through `HudMeshPatchTargets` using declared types and exact
signatures. Core Harmony registration excludes `HudMeshCapturePatches`; after core
components exist, that class installs under its own Harmony owner in an exception
boundary. No curve owner registers until optional installation succeeds. An optional
failure retains flat HUD geometry without aborting F3/OpenXR/controller initialization.

## Current 0.4.9 presentation layer

`VrAvatarVisuals` now owns only renderer visibility and equipped tool root placement;
all native arm IK/bone/body scaling is removed. `VrGloveVisuals` extracts cached local
game glove geometry into persistent mod-owned MeshRenderers, separate from animation,
physics and semantic tracking. `VrHeadVisibility` suppresses explicit helmet meshes,
not head bones. Early/frame-end restoration continues for native tool transforms.

`VrToolPresentation` registers game adapters and exposes live tool range/mask data.
`ControllerDebugRays` defaults to contextual right-hand range guides, with diagnostic
left/right rays available separately. It does not alter native targeting eligibility.
`VrHudCurve` captures only managed UI/TMP mesh submissions for registered helmet roots,
copies all vertex channels and applies shallow Z curvature after the UI rebuild.
`VrUi` widens only those roots; menus/popups/loading retain their established layout.
See VALIDATION.md for exact data sources, hook counts and hardware limitations.
Historical baseline/earlier iteration sections below retain their original scope.

## Findings

The baseline already uses Unity OpenXR. It does not use SteamVR/OpenVR for rendering and does not need an OpenVR-to-OpenXR replacement. Its Unity dependency project pins Unity **2020.3.17f1**, OpenXR **1.2.8**, XR Management **4.2.1**, Input System **1.0.2**, and HDRP **10.6.0**. These stay pinned for this milestone; a newer Unity XR package cannot safely be dropped into an older game's player independently of its native provider, managed assemblies, settings and assets.

The original implementation has a rotation-only headset camera, detached HUD and ordinary game input. No controller motion drives gameplay. Only Oculus Touch is enabled in its serialized OpenXR settings, although the bundle contains other standard interaction features.

The installed game examined on 2026-09-23 reports build `1263554-JocularReactor`, dated 2026-01-27. UnityPlayer's file version is `2020.3.35.1631451`. Game symbols below were inspected in the installed `BBI.Unity.Game.dll`; proprietary decompiled source is not included in this repository. Build input hashes are recorded by the build script so a different game update can be identified.

## Runtime path

```text
winhttp.dll / doorstop_config.ini
    -> BepInEx preloader
        -> ShipbreakerVrPatcher.Initialize: stage native XR provider + subsystem manifest
    -> ShipbreakerVrMod.Awake: settings + Harmony patches + XR manager + debug component
        -> LynxCameraController.Start postfix: create child VR view
        -> CanvasScaler.OnEnable postfix: attach VrUi once
F3 -> XRGeneralSettings bundle -> standard profiles -> InitializeLoaderSync -> StartSubsystems
OpenXR display -> child Camera stereo rendering
OpenXR HMD -> legacy TrackedPoseDriver -> child Camera rotation
OpenXR controller device actions -> IVrTrackingProvider -> ShipbreakerTrackingSpace -> debug rays
InControl -> LynxControls -> existing cutter/grapple/locomotion (unchanged)
```

### Camera and head pose

`Patches.CreateVrCamera` hooks `LynxCameraController.Start`. The current game's `Awake` sets the static `MainCamera`, `MainCameraTransform`, `VirtualCamera` and `MainBrain`. Its main camera is serialized and should not be assumed to live on the controller GameObject. The patch now prefers that static camera and retains the component lookup as fallback.

`VrCamera.Create` creates a child camera, copies clip distances and culling mask, puts the eye at local `(0,0,0.2)`, and adds `UnityEngine.SpatialTracking.TrackedPoseDriver`. The pose source is explicitly GenericXR/Center and tracking remains **RotationOnly**. The original camera still supplies body/gameplay orientation, while the child supplies headset look. F3 chooses which camera renders. This is not full positional head tracking; adding that would change the baseline's behavior and needs its own collision/comfort work.

`ModXrManager` previously called both XRManager and the active loader's initialize/start routines, then reflected a private `isInitialized` member. The pinned OpenXR 1.2.8 source instead tracks its loader state internally; this private-member dependency is fragile. The candidate uses XRManager lifecycle methods once per transition, checks the public display subsystem's `running` state, restores non-VR rendering while it is stopped, and logs recoverable startup failures. F3 toggles the requested session state, including cancelling a start still waiting for a headset. Destroying the manager stops/deinitializes its loader.

### Controller plumbing

`Tracking/IVrTrackingProvider` (declared in `TrackedPose.cs`) exposes tracking-space `Head`, `LeftHand`, and `RightHand`; each hand has independent grip and aim poses plus a trigger value used only for ray color. It contains no Shipbreaker types. Position units are metres and rotations use Unity's coordinate convention.

`OpenXrTrackingProvider` reads the HMD center-eye pose through Unity's XR feature API, matching the legacy camera driver. It reads OpenXR `devicePose` and `pointer` PoseControls from Input System's current left/right XR devices. OpenXR 1.2.8's interaction features register their device actions before the session starts; no late native action set or second OpenXR instance is created by the mod. Devices are resolved on every sample, allowing reconnects without retaining a stale handle. Each pose must be tracked and have both position and rotation; nonfinite values and a zero quaternion are rejected. Aim does not silently fall back to grip.

`OpenXrProfiles` enables only the five standard controller features present in the existing bundle: KHR Simple, Oculus Touch, Valve Index, HTC Vive, Microsoft Motion. It runs before loader initialization. Mock runtime, conformance, eye gaze and platform-specific features are not newly enabled. Profile registrations stay in the runtime adapter, outside gameplay.

On the tested Pico 3 / Steam Link setup, Input System receives updates but exposes no devices. `NativeXrHandReader` therefore reads the existing provider through Unity's XR SDK when the corresponding Input System controller is absent. It requires actual pointer position/rotation features, never grip substitution. This old provider exposes both poses' validity under duplicate shared `IsTracked`/`TrackingState` names. Version 0.3.4 permits shared device flags only when both independent pointer validity fields are absent and both shared flags are available. Partial independent metadata is rejected. Shared flags cannot distinguish aim-only tracking loss from grip tracking; this compatibility path is for diagnostics and must not be promoted to tool aiming without resolving that limitation. Whole-device invalidity or incomplete/nonfinite poses still hide rays.

`InputSystemUpdateBridge` pumps at most once per frame only after two frames without host callbacks, while focused VR diagnostics are active. Captured hardware logs show normal host callbacks and zero fallback updates. Visibility logs distinguish configuration, VR state, game-window focus and camera gates from controller pose validity.

Unity's [OpenXR 1.2 input documentation](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.2/manual/input.html) describes the profile/device mapping and startup binding constraint. This implementation uses the existing profile-defined device actions; a later gameplay action layer should bind explicit OpenXR layouts before session startup.

### Aligning controller positions to a rotation-only head

`ShipbreakerTrackingSpace` is the sole game-space adapter. For body position `B`, body rotation `R`, eye offset `E`, tracked head position `H` and controller aim pose `(P,Q)`:

```text
world position = B + R * (E + P - H)
world rotation = R * Q
```

Subtracting the current head position compensates for the child camera not translating with the real head. Both hands and head must be in the same runtime tracking space. The child camera's HMD rotation is not applied again. This preserves physical hand-to-head position, including seated/standing height, within the baseline view model. It does not introduce hand locomotion or positional head movement. Common tracking-origin translations cancel; arbitrary runtime recenter rotations still need hardware verification.

`ControllerDebugRays` samples in LateUpdate and before rendering. It draws two 2m LineRenderers, cyan left and orange right; trigger pressure fades them toward white. The material is bundled from HDRP's own Unlit shader. Rays have no colliders, raycasts, damage, input injection or game actions. They hide on F3-off, invalid head/aim, disabled configuration, inactive camera, lost application focus or component disable. A rendering/input exception disables the debug component's work and logs the problem without deliberately shutting down baseline VR. Tracking changes are logged once per transition. The native runtime's tracking/focus behavior still needs a headset acceptance run.

### Existing input and exact aim origins

The mod's only original inputs are `UnityEngine.Input.GetKeyDown`: F3 toggles VR, F5 selects quality level 0, and F6 toggles scene lights. The new debug code does not call these gameplay input paths.

The game uses **InControl** through `BBI.Unity.Game.LynxControls` and `GameplayActions`. `InputHandlerGameplay.Update` dispatches configured game actions; `BBI.LynxPlayerController` handles the player's movement. Ordinary keyboard, mouse and gamepad bindings remain in charge.

| Game system | Verified origin / direction | Future adapter boundary |
|---|---|---|
| `ScalpelController` | `Physics.Raycast(LynxCameraController.MainCameraTransform.position, ...forward, ...)` | Replace only the targeting pose when a cutter milestone is authorized; retain heat, damage and state handling. |
| `CuttingController.TryGetCutLineTargetables` | Builds screen-space cut samples around `ScreenCenter`; converts each with `MainCamera.ScreenPointToRay`. | A split-saw needs an entire hand-oriented cut plane/sample grid, not just one forward vector. |
| `CuttingController` cut execution | Cut plane normal derives from a cross product of main-camera forward and transformed cut rotation; ranges are measured from the main camera. | Keep target selection, plane orientation, distances and FX aligned. |
| `CuttingController` single-target query | Raycasts from `MainCameraTransform.position` along `.forward`. | Share one validated tool aim source with the cut-line path. |
| `CuttingToolController` | Orchestrates cutter/scalpel state, mode switching, cooldown and equipped visuals. | Do not replace these mechanics for pose injection. |
| `RaycastSystem.OnUpdate` | Uses `LynxCameraController.MainCamera` position/forward plus `PrepareScreenDualRaycastCommandsJob` screen-grid samples and camera-based scoring. | Dedicated grapple adapter must handle the center ray AND screen-grid selection/scoring. |
| `GrappleRaycastSystem` / `GrapplePushRaycastSystem` | Specialize the shared raycast system's masks/grid sizes. | Leave shared raycast services intact until all affected interactions are understood. |
| `GrapplingHook` | Consumes scored raycast results; push/throw force uses `MainCameraTransform.forward`; screen-space snapping uses `MainCamera.WorldToScreenPoint`. | Changing only the visual rope/muzzle would leave targeting and push direction wrong. |

`CutterScript` in Assembly-CSharp is not the authoritative gameplay cutter path identified above. No tool methods are patched in 0.1.

### Harmony and UI

The only Harmony targets remain `LynxCameraController.Start` and `CanvasScaler.OnEnable`, both postfixes. Parameters now use Harmony's `__instance` injection name. `Start` is private in the installed game, so the patch uses the string method name rather than `nameof` on an inaccessible member. Camera/UI component creation is guarded against duplicates.

`VrUi` records canvas position, rotation, scale, render mode and prior scaler enabled states. While VR is enabled it changes root screen canvases to world space at scale .001, disables the two scaler components and follows the original game camera 1.5m ahead. It restores the saved values when VR stops. Since 0.3.2 the follower lives on a mod-owned helper object, avoiding insertion of an unknown component into the game's entity-converted canvases. This retains detached HUD behavior; controller-driven UI is not implemented, and menu/zoom/scene-specific behavior remains to be validated.

## Build and assets

The original .NET build used an author-specific RaiManager output path, a game-library NuGet package and uncommitted Unity player exports. The candidate builds to `artifacts`, references the user's installed game without copying it into the release, pins the SDK/reference packages, and uses the checked-in BepInEx/Harmony/Cecil binaries rather than silently combining their versions with different NuGet versions.

`scripts/build.ps1` runs the exact Unity editor in batch mode, builds a dependency player, exports the HDRP ray material bundle, restores locked .NET packages, builds both assemblies, and stages matching native OpenXR files from that Unity player. It does not launch or modify the game. The original `xrmanager` bundle remains intact. `ShipbreakerVrPatcher` retains upstream native-file copying at game startup; this makes backing up those destinations part of the install/rollback procedure.

The [file inventory](FILE-INVENTORY.md) covers every upstream tracked file, including serialized settings, metadata and bundled binaries. AssetBundle Browser is vendored editor tooling, not an in-game system.

## Steam Frame and next milestones

Steam Frame is one controller target within the PC OpenXR runtime. Valve documents [Frame fallback bindings](https://partner.steamgames.com/doc/steamframe/controllers): SteamVR can remap an Oculus Touch binding when a native Frame/Generic binding is absent. This candidate enables Touch alongside the other standard profiles. That is the initial Frame compatibility route; it is **not hardware-verified Frame support**.

A native Frame feature uses `XR_VALVE_frame_controller_interaction` and `/interaction_profiles/valve/frame_controller_valve`, per [Valve's custom-engine documentation](https://partner.steamgames.com/doc/steamhardware/steamframe/engines/custom). Do not invent a profile path or adopt a Unity 2022+ integration wholesale in this 2020 player. A native profile can be introduced later as an optional runtime feature behind the same tracking interface, with runtime-extension detection and fallback. No headset-brand tests belong in cutter/grapple adapters.

After the acceptance checklist passes: add a generic action/haptics contract; adapt scalpel aiming first; then split-saw planes/FX, grapple selection/throw, locomotion, switching, UI and comfort. First verify and record the original VR baseline on this current game build. Source compilation alone does not establish that baseline or complete milestone 0.1.

## 0.3.5 habitat camera/UI correction (pending hardware test)

The camera and UI descriptions above describe the initial milestone design; 0.3.5 changes their ownership and selection. The VR rig now persists outside game camera hierarchies and copies the selected camera's body transform after Cinemachine, retaining the same 0.2m eye offset and rotation-only tracked child. `Hab3DController.Awake` registers the habitat source through its public CinemachineBrain, and a prefix on `LynxCameraController.OnGameStateChangedEvent` releases camera suppression before the game applies new enabled states. Hab (and pause from Hab) selects the habitat brain's output; other states select the yard source. Clip planes/culling mask are refreshed per frame, with converted canvas layers included only on the VR camera.

Unity documents the brain's [OutputCamera](https://docs.unity3d.com/Packages/com.unity.cinemachine@2.6/api/Cinemachine.CinemachineBrain.OutputCamera.html) as its controlled Unity camera. Converted canvases explicitly assign the VR [worldCamera](https://docs.unity3d.com/2020.3/Documentation/ScriptReference/Canvas-worldCamera.html), which is also the event camera for world-space UI. This does not add controller menu input. Screen canvases retain their pixel rectangle at conversion, use a two-metre panel width and retarget when the selected camera changes. Original world-space canvases are not converted. Existing HUD rendering, normal mouse targeting, both-eye visibility, native habitat navigation and F3 restoration need hardware validation after these changes.
## 0.3.6 UI depth and transition handling (pending hardware test)

`VrUiMaterials` copies final CanvasRenderer materials during `Canvas.willRenderCanvases`, registered after CanvasUpdateRegistry. Working on final materials preserves Unity UI/TMP stencil variants instead of mutating a shared font or game material. Standard Unity UI/TMP shaders use `unity_GUIZTestMode` for their depth comparison (see Unity's [TMP shader source](https://github.com/Unity-Technologies/NotificationsSamples/blob/master/Assets/TextMesh%20Pro/Resources/Shaders/TMP_Bitmap-Mobile.shader)). The copies set that uniform and supported explicit depth properties to Always, and use the Overlay queue. Both regular and pop-material slots are handled and restored. Custom shaders may not honor these controls; their names are logged for follow-up. Existing sorting/stencil behavior still needs visual verification.

Registered screen canvases that become world-space before setup retain native transforms/scalers and get only material/event-camera handling. Originally world-space, unregistered scene objects remain untouched. The persistent rig now supplies `BodyTransform` to the UI and controller mapping, allowing them to keep rendering at the last body origin during a gap between game cameras. This preserves head rotation and the existing eye offset; it does not unblock a stalled Unity main thread or bypass the game's loading/wake-up flow.
## 0.3.7 correction to UI rendering

The 0.3.6 Overlay queue override was incompatible with HDRP's world-space transparent draw ranges and has been removed. Material copies now preserve their source queue (3000 for the observed UI/TMP shaders), while retaining the depth-test overrides. The 0.3.6 section above is historical, not a recommendation to use queue 4000. `VrUiDiagnostics.Capture`, bound to F7, provides camera/HDRP/canvas state for the still-unresolved loading noise. It only logs observations; it does not modify render settings or game progression.
## 0.3.8 headset submission ownership

`VrCameraSubmission` uses HDRP's begin/end frame callbacks to prevent other display game cameras from creating XR passes while the mod's view is active. The game's loading camera remains enabled for its normal desktop/effects behavior, but its `HDAdditionalCameraData.xrRendering` flag is suppressed only for that render. Existing true flags are restored at the end, on disable, and at the next begin callback for interrupted renders. Originally false flags, render-texture cameras and non-game cameras remain untouched. Cameras without explicit HDRP data are logged and skipped. No additional component is inserted into game-owned entities. `TemporaryBooleanOverrides<T>` owns the reversible flag lifecycle and is covered by managed tests. No changes apply before F3 enables VR.

The F7 evidence, verified HDRP callback ordering, rationale and outstanding hardware test are recorded in VALIDATION.md. UI layout, material depth settings and controller action behavior are unchanged from 0.3.7. F7 now prints small canvas scales with five decimal places to avoid rounding a valid 0.001-scale panel to an apparent zero.
### Candidate 0.3.9: separate loading effects from loading text

VrUi recognizes the captured LoadScreenCanvas / LoadScreenCamera pairing and owns VrLoadingBackdrop for that panel. This game-specific adapter uses a mod-owned mono Camera with copied HDAdditionalCameraData and cullingMask=0 to render only clear/post effects into a no-alpha texture. The texture is displayed on a head-following RawImage at world-space UI order 32766; the loading canvas temporarily uses 32767. Both use the loading canvas sorting layer, and the original canvas order is restored on release. The camera renders before VrCamera and never submits XR. This keeps CRT/VHS effects behind stereo text instead of applying the old loading camera over the entire headset image. The texture background and text are separate draws, so text is not processed by the loading camera's effects. Original cameras, profiles, assets and game loading logic are unmodified. VrUi teardown/F3-off disposes the capture, image, material and texture; allocation failures fall back to 0.3.8's readable UI. Headset appearance remains pending validation.

### Candidate 0.4.0: first game-specific Stinger adapter

Generic TriggerLatch implements context-gated, release-to-arm trigger edges with hysteresis; VrStingerControls owns OpenXR sampling and a mod-owned aim Transform. StingerPatches replaces exactly nine calls in six Stinger-only methods: UpdateTargeting (2 camera-transform reads), ScalpelTarget.Reset (3), ready/cutting/disabled fire reads (1 each), and FX UpdateLine (1 muzzle TransformPoint). Original global camera/action objects stay intact; helpers fall back to original behavior outside VR Stinger mode. The FX origin change also moves the existing burning capsule origin, avoiding a mismatch between the visible ray and damage segment. Existing locks, equipment, target validation, heat and vaporization execute in game code. No changes to Splitsaw or grapple. Sampling is once per game frame, independent of the debug-ray toggle; known legacy shared grip/aim validity limitations still apply. Model alignment and further action mappings are subsequent work. See CONTROLS.md.

### Candidate 0.4.1: movement actions above controller samples

ControllerInputs adds stick availability, stick vector, primary/secondary buttons, click and grip to TrackedHand. Both OpenXR Input System and native XR SDK readers populate the same device-neutral struct. MovementInput resolves configurable semantic VrButton bindings and a radial deadzone. MovementNeutralGate requires centered sticks/released mapped buttons after context or tracking loss; triggers are independent, permitting movement while cutting.

VrMovementControls maps those inputs to the game's directional actions and checks the gameplay action set plus per-direction action availability. MovementPatches replaces input reads only in ThrustController.Update (15 calls) and OrientationController.HandleAxisRotation (14 calls). Unmapped actions use the original reader. The scoped LastInputType adapter selects the original gamepad curve instead of mouse acceleration for sticks; it does not change global device state. Existing thrust forces, fuel, limits, physics, damage/steering restrictions and inertia remain game-owned. Returning controls to neutral stops input, not physical momentum. New config and reader code have no Pico/Frame device-name branch. Native Frame profile support is still absent; see CONTROLS.md for documented SteamVR fallback limitations.

### Candidate 0.4.2: native tool wheel and menu actions

VrMenuControls attaches a mod-owned InControl InputDevice once InputManager is set
up. Its Update samples generic OpenXR inputs before InControl commits devices,
selects the active device and updates PlayerActionSets. It emits gamepad buttons
and menu axes inside the game only; no OS input or controller driver is involved.
The original bindings, enabled action sets, per-action locks, UI repeat logic and
EquipmentController/ToolSelectionUIController continue to own behavior. Default
controls are in CONTROLS.md. Existing user gamepad remaps still apply.

MenuInput routes gameplay interaction/tool menu/mode and menu confirm/back/tabs/
context actions separately. Normal gameplay sticks and weapon triggers are never
forwarded by this device. Left stick is forwarded for the native tool wheel only.
MenuNeutralGate requires neutral/released input after game-state, focus or tracking
changes; context transitions cannot carry a held confirm/pause into the next state.
F3-off neutralizes the device without unplugging it. Destruction detaches passively
so the game does not report a disconnected physical controller.

EquipmentController.Update and CloseToolSelectMenu hooks cancel a VR-owned wheel
without swapping equipment on invalid input/context loss. The movement adapter is
blocked/rearmed while the wheel is open; the Stinger latch also resets. Mode and
wheel selection may equip other tools, but their tracked aiming/firing adapters
are still unimplemented. Frame continues through the existing standard profiles;
this change adds no vendor branch or native Frame profile. Runtime acceptance is
pending despite successful local build and 89 policy/math checks.

### Candidate 0.4.3: Pico ergonomics and deliberate pause chord

Tool-wheel selection now forwards the physical RIGHT stick through the virtual
pad's native ToolNav (left-stick) controls. Ordinary menu navigation stays on the
physical left stick, and menu scrolling stays on the right. Holding left Y while
choosing no longer needs two inputs from the same thumb.

PauseStart is no longer bound or read. New PauseChordFirst/Second keys default to
RightSecondary + LeftSecondary (Pico B + Y). The chord requires two distinct,
non-None buttons and release-to-arm. After activation both buttons are consumed
until both are released; releasing/repressing just one cannot pause repeatedly.
The active chord emits only Start, cancels a VR-owned wheel without a tool swap,
and gates movement. Game-state/focus/tracking neutral gates still apply. No trigger
or grip maps to pause by default. The requested Frame binding is D-pad Up + Y,
explicitly not stick-up. Implementing that exact binding awaits distinct native
Frame controls; do not map an aggregated Touch-fallback upper button as D-pad Up.

### Candidate 0.4.4: explicit action-device ownership and early loading view

VrMenuControls now temporarily sets the current PlayerActionSet.Device to the
mod-owned VR pad while input is eligible and armed. This bypasses InControl's
last-active-device arbitration without replacing game action bindings. It captures
and restores the prior Device on eligibility loss, VR-off, set changes and teardown,
only restoring if the value is still mod-owned. Keyboard/mouse bindings still
participate; an ordinary physical gamepad needs Menus.Enabled=false or VR-off for
those actions. InputManager.OnUpdate diagnostics run after native device commit
and PlayerAction update, logging button transitions and action values, enabled
state and bindings. InputManager reset/reattach re-registers the observer.

ModXrManager creates an early rig once the OpenXR display starts, rather than waiting
for habitat/player camera creation. It seeds the body pose from Camera.main when
available. Registered canvases are adapted immediately. LoadScreenCanvas placement
is enforced before UI rendering as well as LateUpdate: a two-metre-wide panel,
1.3 metres from the tracked view, with size/scale corrected after loading animations.
Other panels retain their body-relative placement. The static backdrop remains at
two metres but uses queue 2999, below text/UI queue 3000 and within HDRP's transparent
range. The game's original camera, UI transforms and action-device values are
restored on teardown. No shader, native plugin or game assembly is replaced.

### Candidate 0.4.5: D-pad, Select/View and grapple adapter

MenuInput has separate Dpad and Select outputs. Holding the semantic left-grip
modifier converts the physical left stick to one dominant D-pad direction. A
ShiftedStickGate suppresses that stick's movement until recentered after release,
including when movement sticks are swapped. Select uses a second release-to-arm
ButtonChord (X + Y), outputs virtual Back/View and cancels a VR-owned wheel.
Pause has priority. Both chords gate movement, Stinger and grapple input; grapple
push charge is canceled before its release can produce a blast.

VrGrappleControls consumes the same generic OpenXR provider and TriggerLatch as
the Stinger adapter. A disabled, monoscopic mod-owned camera copies native camera
projection and uses the right-hand world pose. It never renders. GrapplePatches
replaces 25 calls in five methods: RaycastSystem.OnUpdate (4, only grapple/push
system instances), UpdateGrapplingHook (14 input reads), CheckIfObjectInBounds (1),
OnThrowPressed (3) and OnRangedThrowPressed (3). The shared raycast patch preserves
other interaction systems while supplying the grapple center ray, screen-grid
matrix and scoring with the same hand camera. Expected counts fail explicitly
if the game's method bodies change.

GrapplingHook.Start registers the current hook/equipment/rope. LaserRope.StartPoint
uses the right hand only for that rope. GetPositionForceInfo replaces the local
copy of connected manipulator direction, leaving the physical body anchor, spring,
damping, mass and force caps native. Other ropes/tethers are unchanged. Queued
ranged pushes are guarded against invalid context. Loss of context/tracking cancels
push charge and releases an owned grab; release callbacks cannot recursively release.
Right trigger replaces GrappleFire, left trigger replaces RetractionModifier after
attachment. Native AutoRetract retains its modifier-as-brake behavior. Trigger
release forces let-go even with native toggle enabled. No trigger is injected into
the global virtual pad. Haptics, tether mapping and animated tool model placement
are not part of this adapter. Runtime physics still requires hardware validation.

### Candidate 0.4.6: remaining equipped-tool actions and menu bumper mapping

VrAdditionalToolControls registers the equipment/cutter and owns a disabled
monoscopic hand camera with independent primary/alternate TriggerLatch instances.
Splitsaw and demo charges consume right-primary/left-alternate; scanner consumes
right-next/left-previous, keeping scanning head-directed through the VR view camera.
Mode/context changes disarm both triggers. Demo entry into Placement or Detonation
also disarms them, so a held trigger cannot carry across an automatic mode switch.
Native tool locks, inventory, sequence timing and resource consumption remain intact.

AdditionalToolPatches explicitly rewrites 38 sites in 17 method bodies: Splitsaw
input, camera/grid target selection, cut plane, targeting reticle checks, FX origins,
beam collision, demo input and scanner input/ray. Demo PlayerTransform/ThrowOffset
getters use scoped postfixes. A TryPerformCut prefix rejects new cut execution when
the VR adapter owns it but tracking/context is invalid. Already committed native
queued cuts, placements and detonations still execute their native sequence.

VrSplitsawPreview draws mod-owned stereo line guides from actual cut-line width,
offset and rotation using the same hand projection at two metres. While available,
CuttingUIController.Update hides only the old cut-line/edge markers; other warning
and heat UI stays native. Guide availability depends on the existing debug material,
not the debug-ray toggle. Loss of context hides guides. F3-off restores native UI
on its next normal update. Each guide host is scene-owned and cleans up its material.

TetherPatches adds 16 replacements in seven methods to VrGrappleControls: press,
release, center/grid rays, candidate scoring, preview endpoint and barrel effects.
Tether.Update registers the active controller and cancels placement on invalid
context/left tracking/action state. TriggerLatch.Released is only a valid falling
edge; invalid/context-loss samples never generate commit edges. The left trigger
routes to tether placement only with no grabbed object and no held grab trigger;
otherwise existing retraction handling applies. Right trigger cancels a preview
and is consumed for that frame so it cannot also start a grapple grab.

Menus use new MenuLeftBumper/MenuRightBumper/MenuX/MenuY settings, deliberately
superseding the old defaults without rewriting user config. LT/RT page/cycle inputs
are forwarded only outside gameplay. Grip+D-pad routing is now gameplay-only;
menu sticks stay navigational and grips are dedicated tab bumpers. Entry/recovery
neutral checks include held triggers. View/Back aliases retain the existing chord;
diagnostics include committed aliases and native WorkOrder/PlayAudioLog bindings.
No legacy task panel is force-opened. InControl offline tests confirm the aliases
and menu bumper/face/trigger outputs; task UI availability remains game-controlled.

ChordMemberDelay reserves each Select chord member for 120 ms before forwarding a
single held action. Quick taps are emitted once on release. Recognized commands
and context loss clear pending taps, avoiding a defaults/interact/tool action
before a near-simultaneous X+Y. The semantic button configurations remain in use.

## 0.4.7 adapters

`Tracking/ToolProjection.cs` is game-independent normalized pixel-to-angular-ray
math. `VrAdditionalToolControls.ScreenRay` adapts LynxCameraController's logical
screen size; only explicit Splitsaw/scanner call sites and the preview use it.
`DemoChargeAimPatches` snapshots the hand pose in a scoped ThrowDemoCharge call,
registers the returned StructurePart (excluding save-load overrides), and resolves
its entity before DemoChargeInitializationSystem.OnUpdate. Only Direction is
changed; entries are removed after application, destruction, world mismatch or timeout.
`VrDemoReticle` owns local-plane UI rotation and restores native world rotation
when leaving VR. It does not move the HUD reticle onto the hand.

`Tracking/VrToggleGesture.cs` contains device-independent hold/rearm logic.
`ModXrManager` reads it early even while the VR view is off. Warm session lifetime
is separate from view lifetime. `ScreenDimensionsPatch` refreshes the game's
otherwise stale screen-size cache; `VrCameraSubmission` excludes desktop cameras
from XR submission while the runtime remains connected.

`VrAvatarVisuals` is a game-specific render adapter, not a physics/body controller.
It matches IKLimb.Target to GrabController's exact left/right HandPositionTransform,
uses the existing upper-arm/forearm/hand bones and native target-to-wrist correction,
and solves reach via reusable `Tracking/ArmGeometry.cs`. No reparenting or extra
components on game entities. It records local transforms and renderer flags at
beginCameraRendering for the VR view and restores at endCameraRendering, endFrame,
next Update, exception or disable. Body scale is only applied to a verified visual
animator subtree; tool roots come from concrete game fields, never name searches.
Scale defaults are initial fitting choices; game assets and world/pose scale remain
unchanged. Demo/scanner model fitting, finger tracking and physical grab input are
not implemented by this pass.

## 0.4.8 rendering and habitat corrections

`VrUi.ApplyPlacement` reasserts all owned screen-space roots (including scalers,
layout, mode and scale), rather than only loading panels. Child menu animation and
native visibility remain game-owned. `HabNavigationUiPatch` replaces only the
native UpdateNavLocation projection: normalized mono viewport to panel coordinates,
Z=0; native navigation/actions are untouched.

`VrAvatarVisuals` now resolves exact hand targets from legacy IKLimb or Animation
Rigging TwoBoneIKConstraint, then humanoid bone mappings if available. It applies
poses in LateUpdate after the VR rig (9500 versus 9000), before skinning, and restores
at frame end or early Update/FixedUpdate (-9000). This supersedes the 0.4.7 per-camera
callback timing; secondary cameras during the same VR frame see the fitted pose.
Skin update flags and expanded bounds are also restored. `VrHeadVisibility` registers
HelmetController's explicit mesh after creation/swaps, hides renderers temporarily,
and collapses identified local head bones during drawing. Flashlight, warning light,
canvas HUD and inventory state are untouched. New grapple-only scale/offset controls
preserve cutter fitting and gameplay aim.
