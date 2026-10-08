# Grapple muzzle measurement — 0.4.19

Inspected locally from the installed game using UnityPy 1.25.3. No extracted game
assets are included in the mod/package. The Unity editor batch probe could not
access its licence; a read-only asset reader was used instead.

Source bundles under Shipbreaker_Data/StreamingAssets/aa/StandaloneWindows64:
- player-base_assets_all_b874acec1e679fbe4b04329a0fe0fd3b.bundle
- player-tools_assets_all_b76c76ea123590c4b6e0a9092e7867d7.bundle

Prefab: Assets/Content/Prefabs/Player/PRFB_GrappleGun.prefab.
The circular cap is mesh GB_GrappleTool_Piston_2, path ID 4073060094173292823.
Hierarchy below the gun root:
SK_GBGrappleTool_01_AnimationLoop / SK_GrappleTool_Root /
SK_GrappleTool_Piston / GB_GrappleTool_Piston_2.

Native mesh bounds center: (-0.000000001863, 0, 0.000000014901).
Native extents: (0.037937484682, 0.037937477231, 0.125349491835).
Its front (+Z) cap has 20 perimeter vertices plus a center vertex. Perimeter
radius is 0.03232172–0.03232180 metres; its center matches bounds.center.x/y.
Thus (bounds.center.x, bounds.center.y, bounds.max.z) locates the actual cap
center. This is a verified property of this specific mesh, not a general rule
that arbitrary mesh bounds identify a muzzle.

In unscaled gun-root coordinates, the cap center is approximately:
(0.0076626753, 0.2030670154, 0.6886989772).
The authored Gun Barrel marker is (0.001, 0.215, 0.709).
Difference: (+0.0066626753, -0.0119329846, -0.0203010228) metres.
At the configured 0.55 visual scale, this is 3.664 mm right, 6.563 mm down and
11.166 mm back. These directions agree with the user's left/high ray report.

Implementation keeps the gun pose and native targeting intact, follows the
identified animated mesh transform, and changes the visual beam origin only.
It uses the real transformed mesh point once the model is posed for rendering;
before posing, it applies the same root pose/scale to that mesh point. The barrel
marker is retained as fallback when the mesh cannot be found. No arbitrary 2 mm
trim or gun-position change was applied.

Local inspection evidence is under workspace work/muzzle-inspect (mesh data,
transform hierarchy and a wireframe view). Headset verification is still required
for runtime animation/rendering behavior; asset measurements alone do not prove
that every rendered frame is correct.
