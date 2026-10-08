# Structural scanner audit - 2026-09-24, candidate 0.4.9

Scope: review the supplied October/November 2025 discussion against the locally
installed game and current mod. No new runtime fix, rebuild or installer change.
The user is testing 0.4.9 while this audit is performed. This is a source audit,
not an observation of scanner rendering in the headset.

## Findings

| Reported issue | Status in 0.4.9 |
| --- | --- |
| Room names missing or displaced | Confirmed unadapted coordinate path remains; expected to be broken under converted world-space canvases. Not headset-verified this turn. |
| Pressure icons/colors missing | No specific fix or acceptance test. Native pressure state propagation/display logic remains intact; do not infer that its data is missing solely from an invisible icon. |
| Blue scanner effect reduces label readability | Not specifically addressed. Existing UI depth overrides prevent geometry occlusion, but do not guarantee independence from scanner effects or post-processing. |
| Scanner controls and object selection | Adapted: mode inputs and Scanner.HandleRaycasting use VR input/view projection. These do not affect room-marker ECS jobs. |
| General HUD depth/layout | Adapted, including habitat/loading fixes, but not a conversion of each marker's pixel-space position. |

## Exact remaining paths

`UIMarkerTrackPositionSystem.OnUpdate(JobHandle)` obtains position, forward,
projection matrix and world-to-local matrix from the original LynxCameraController
MainCamera. The mod does not replace those accesses. Head-only turning therefore
is not represented in this room-marker visibility/projection path.

Its Burst-compiled `CalculateUIMarkerPositionJob.Execute` reads CenterOfMass.World,
LocalToWorld or Translation. It applies behind-camera/distance checks, emits
UIMarkerShow/UIMarkerHide, converts world coordinates to screen pixels, and emits
UIMarkerSetPosition with Z=0. It also emits distance-dependent UIMarkerSetScale.

`UIMarkerSetPositionSystem.OnUpdate` is managed code. Both its position-writing
branches assign UIMarkerSetPosition.Value directly to RectTransform.position.
After the mod changes the root canvas to world space, those pixel coordinates are
still assigned as world positions. Changing root scale, HUD curvature, camera pixel
dimensions or material depth tests does not resolve that coordinate mismatch.

Two-dimensional coordinates are not inherently unusable in VR: they can be mapped
into a correctly positioned panel. The defect is the missing coordinate conversion,
plus the camera/visibility mismatch, not the mere existence of a 2D intermediate.
For room-centered stereo markers, retained room world positions are a cleaner source.

`InitRoomScannerInfoSystem` instantiates the original RoomInfoMarker prefab, sets
its localized room name, links UIMarkerTrackPosition and AirPressureStateSource to
the room, and initializes distance scaling. This relationship is available to a mod.

`UIRoomInfoMarkerUpdatePressureSystem` propagates room pressure state when enabled
or changed. `UIRoomInfoMarkerUpdateSystem` also reads the current pressure directly
from the source room and calls RoomInfoMarker.UpdateInfo. The latter switches native
pressurized/unpressurized/cycling/breaching/breached visual GameObjects. It does not
simply set one text color. A replacement label which omits these children/state links
could explain another mod's missing colors, but that is a hypothesis, not a diagnosis
of the pasted author's implementation. RoomInfoUIController is a separate current-room
HUD display and must not be confused with the floating structural scanner markers.

An additional unadapted path exists in ScannerUIController.HandleScanning:
WorldToScreenPoint is assigned directly to the radial-fill transform.position.
This needs checking when that scanning-progress state is exercised.

## Feasibility and next implementation boundary

There is no source evidence for the claim that a mod cannot fix this or that the
installed BBI.Unity.Game.dll must be overwritten. The scheduling, managed UI update,
marker creation and RoomInfoMarker.UpdateInfo boundaries are accessible to the same
runtime patching approach already used by this project. Directly patching Burst
Execute is not required; changing its managed inputs or using a VR-specific marker
adapter after the appropriate synchronization point are candidate approaches.

A focused fix should preserve the original room/entity links and pressure widgets,
use the VR view for visibility, retain valid distance/behind-camera behavior, and
place/billboard room markers in a dedicated stereo presentation separate from the
widened helmet HUD. Preserve native scanner-mode activation and flat-screen behavior.
Evaluate scanner-overlay rendering separately after labels and pressure indicators
are correctly placed. No claim of a completed or headset-proven fix is made here.

## Headset checks to add

- Enable structural scanning with its upgrade unlocked: room names and pressure
  indicators should appear for appropriate rooms.
- Turn only the headset, then rotate the player: compare marker placement/visibility.
- Compare the same rooms with VR toggled off and on.
- Where possible, observe pressurized, decompressed and transitioning room states.
- Check text/icon readability against the blue scanner effect and room geometry.
- Check object-mode targeting/tooltips and scanning-progress UI separately.

Evidence: exact installed game types inspected locally in work/; proprietary
reconstructed source is not copied into this repository or update package.

Inspected BBI.Unity.Game.dll SHA-256: 2DB344787C1492E148DDA4D411C115C11714A9894777171626B78A2EE0BD7C8E

## 0.4.12 follow-up

User confirms ordinary scanner descriptions are covered by wire geometry in 0.4.11.
0.4.12 corrects managed UI sorting precedence and shares helmet/scanner anchors.
This targets object tooltip readability, not the ECS room-marker pixel/world mismatch.
The latter and pressure-state rendering still require their own implementation/test.

User feedback after 0.4.12: scanner descriptions now work. This accepts the ordinary
object-description visibility correction on Pico/Steam Link; structural ECS room
markers/pressure icons have not received equivalent acceptance.

## 0.4.20 implementation

The new adapter changes the four managed camera accesses feeding the native marker
job to the VR view, retaining native mode/distance/behind-camera rules. It intercepts
the managed position/scale assignments only for RoomInfoMarker roots, reads the
linked room's CenterOfMass/LocalToWorld/Translation plus optional world offset,
and places/billboards that existing marker in world coordinates. It compensates
for the HUD parent's scale and reapplies room placement after the parent follows
the head. Those marker graphics bypass helmet mesh curvature. The existing room
name localization and pressure-state widgets/update systems remain intact.

The unrelated ScannerUIController scanning-progress radial-fill path is not changed
in this candidate. Ordinary scanner tooltip sorting from 0.4.12 is retained.
Runtime room-label/pressure acceptance is pending. No claim of headset-proven
structural scanning or pressure-color correctness is made before testing.

User feedback on 0.4.20: Structure scanner shows room labels. This confirms label
visibility; pressure indicators and their state changes, anchoring during head
turns, and mode/VR-toggle restoration remain separate acceptance checks.

Further user confirmation on 0.4.20: pressure indicators are visible as well.
Room labels and pressure-indicator visibility are now accepted on Pico/Steam Link.
Pressure-state transitions and lifecycle edge cases were not separately reported.

## 0.4.27-beta2 stereo report review - 2026-10-01

Reviewed publication commit `e6d95c539f7c83e2d9f9a63e774a671d7bd21ac8`.
A tester reports stereo-incorrect scanning on Quest 3 through VDXR, with only
the starting scanner mode unlocked. The affected visual (ship highlighting or
HUD panel), eye-specific symptom and runtime capture are not available. This
is a source/asset inspection, not a reproduced headset defect or verified fix.

Findings:

- The existing structural-room adapter is not evidence that the starting
  scanner's ship highlighting is stereo-correct. Those are separate paths.
- Native `HighlightComponent.ApplyMaterial` replaces ship renderer/MeshCache
  materials. Inspection of the installed scan-mode material bundle resolves
  `Scanner_Object_General`, `Scanner_Object_General_Small` and
  `Scanner_Background_ObjectMode` to `Wireframe/Simple/Lynx`. Its compiled
  parameter metadata uses legacy `UnityPerFrame` / `unity_MatrixVP` and
  `UnityPerCamera` / `_WorldSpaceCameraPos`. Normal HDRP materials use HDRP's
  separate camera constant buffer. The mod does not replace these scanner
  shaders or add a scanner-specific stereo render adapter.
- The project's OpenXR setting selects MultiPass. Missing instanced-stereo
  shader variants alone therefore do **not** prove this report's cause.
  The installed HDRP `XRPass.StartSinglePass` also updates view/projection
  matrices for multipass (despite that method's name). A blanket claim that
  the scanner always receives a mono matrix would be incorrect.
- A concrete risk remains in native `HUDCustomPass.Execute` and
  `DrawUICameraCustomPass.Execute`: they switch camera state for a UI camera,
  then restore `SetupCameraProperties(hdCamera.camera)` without stereo
  arguments or explicitly restoring the current XR pass matrices. If such a
  pass executes before scanner geometry with no subsequent XR matrix reset,
  legacy scanner materials can receive different camera state from HDRP
  materials. The relevant active pass/injection order and resulting GPU
  state must be captured before identifying this as the reported cause.
- Converted HUD materials retain their original shaders. Their depth/sorting
  overrides address visibility, not every possible shader stereo issue.
  The separate scanning-progress radial-fill pixel/world mismatch documented
  above is still unadapted; there is no evidence it caused this report.

Next verification: capture both eyes in the same stationary scene with the
starting scanner off/on, keeping a nearby recognizable ship edge in view.
Check whether the ship silhouette/highlight changes apparent depth or differs
between eyes, versus only the HUD text panel. Record the actual render mode,
active custom passes/injection points and the legacy versus HDRP view-projection
state at the scanner draw. Repeat on SteamVR if available to distinguish a
runtime-dependent symptom from a shared rendering defect. VDXR is currently
the reported test environment, not an established cause.

No runtime code, installed mod, release archive or published commit was changed
by this review. Proprietary shader metadata and reconstructed game sources
remain outside the repository.
