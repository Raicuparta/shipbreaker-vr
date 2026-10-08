# Helmet damage candidate 0.4.32

Status: the user accepted the 0.4.32 damage fix after headset testing. Prepared
for portable 0.4.32-beta1; broader headset/runtime coverage remains unverified.

## Verified effect mapping

Read-only inspection of the shipped effect prefabs and their PlayerFX action
references distinguishes three visuals. Treating every mesh as the red critical
layer was incorrect and caused the oversized white crack in 0.4.31.

| Native effect | Visual | VR handling |
| --- | --- | --- |
| FX_VisorCrack_Small_01 through _05 | White SpriteRenderer cracks | Preserve native angular size, offset, random sprite and roll on the head-following 20 m apparent-depth surface. |
| FX_VisorCrack / Quad | Larger white crack, HDRP unlit texture | Preserve native angular size too; no full-FOV stretch or 2x enlargement. |
| FX_ShatteredGlass_Helmet_01 / polySurface3 | Critical curved glass mesh with red lights | Register separately. Fit native mesh width/height to both eye frusta with 2x margin for the rounded perimeter; follow tracked view. |

The critical model retains its native mesh, UVs, material, lights and physical
depth. Render-time transform overrides adjust width/height and centering. Child
light range/intensity scales with the fitted size. All transforms and light
values are restored at frame end and before the next game update. It does not
replace the curved model with its unwrapped texture, recolor white cracks, or
change native damage thresholds, FX activation, pooling or repair logic.

Full-view Damage Overlay/FadeScreen images retain the regular eye-frustum
coverage; the speculative global 2x textured-image scaling from 0.4.31 is removed.
Native particle graphs accompanying the white crack remain game-owned.

## Earlier test results

- 0.4.28 was withdrawn after input stopped responding. The log started with a
  legacy InControl joystick table bounds exception and then pending-tick errors.
- 0.4.29 added bounds protection without changing OpenXR bindings. The user
  confirmed input and repair worked. Damage rendering fell back to native because
  legacy camera stereo getters did not produce valid eye data.
- 0.4.30 reads projection/view pairs from XRDisplaySubsystem render passes, as
  HDRP 10.6 does. Missing data is retried. The user confirmed head following.
- 0.4.31 preserved small white sprite size but incorrectly enlarged the larger
  white mesh. The separate critical shattered-glass prefab was not registered,
  explaining why the red effect remained unchanged. 0.4.32 corrects this mapping.

## Validation and limits

Release build and 388 managed checks pass. Checks include native angular-size
invariance, asymmetric/canted eye projections, missing eye data, distinction of
the actual critical prefab from both white-crack variants, critical coverage
sizing and preservation of the depth axis. Managed checks do not verify rendered
appearance, lighting, binocular comfort, repair or pooling in a live headset.

The input regression uses a private InControl test copy with a deterministic clock
and calls production bounds prefixes directly. Installed game assemblies remain
untouched. See BUILD.md for preparing that test fixture. The input safeguard is
unchanged from the user-tested 0.4.29 build.

## Headset acceptance

1. Confirm both early and later white damage stages retain their original sizes.
2. At critical damage, check that the red glass covers the view without its outer
   circular edge intruding, follows head turns, and retains the expected lighting.
3. Repair and confirm all damage visuals disappear; repeat after a scene change.
4. Toggle VR off/on and confirm native flat-mode effects and VR effects restore.
5. Check loading/fade transitions, ordinary HUD, input and tool aiming remain usable.

The log identifies critical registration and the applied width/height fit so a
remaining issue can be traced to the actual effect rather than another blanket
size adjustment.
