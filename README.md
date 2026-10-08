# Shipbreaker VR — 0.4.44 beta1

An unofficial Windows PCVR mod for **Hardspace: Shipbreaker**, based on [Raicuparta/shipbreaker-vr](https://github.com/Raicuparta/shipbreaker-vr). Uses OpenXR for headset/controller tracking and game-specific adapters for tools, movement and menus.

Tested during development with Pico Neo 3 over Steam Link and Steam Frame through SteamVR. Other OpenXR headsets may work but are unverified. This is not a standalone Frame game.

## Install or update

1. Install the Steam Windows version of Shipbreaker and run it once normally. Close the game.
2. Extract **the entire portable ZIP** to a normal folder. Do not run it inside the ZIP.
3. Run **Install.cmd**. It finds the game through Steam's libraries, or opens a folder picker. Select the folder containing `Shipbreaker.exe` if asked.
4. Connect the headset and controllers. Start SteamVR with SteamVR selected as the active OpenXR runtime, then launch Shipbreaker from Steam.
5. VR starts automatically. Keep the game window focused, close the SteamVR dashboard and release all buttons/center both sticks before testing.

No Unity editor, developer SDK, separate BepInEx installation or mod manager is needed. Setup does not change SteamVR resolution, game graphics settings or saves. Existing mod settings are preserved; obsolete test keys are removed with a backup. Fresh settings use the accepted HUD/tool calibration, controller navigation and haptics. Automatic performance recording is off.

**Controls:** [Gamepad, Pico and Frame guide](docs/CONTROLS.md). F3 toggles VR/desktop. The controller gesture also works after OpenXR connects: center both sticks, press both stick clicks first, add both full grips and hold all four for two seconds, then release.

## What's included

- Tracked controller tool aim, movement, tool selection, menus and haptics.
- Automatic couch/gamepad mode with native bindings, right-stick aiming, independent head looking and visible aiming rings.
- Consistent 65%-size held tools in both modes, with immediate ordinary tool switching and synchronized tool/heat-bar hiding during grabs and interactions.
- Disembodied tools with calibrated beam origins; stock tool range and game rules.
- Head-following curved gameplay HUD, loading/menu composition and clipped ship cards.
- Structural scanner room labels and pressure indicators.
- Head-following helmet damage, native-sized white cracks and full-view critical shattered glass.
- Native Frame ABXY/D-pad/LB/RB/Menu/View input, plus full-grip shortcuts. Pico retains its own layout.
- UI geometry caching and sorting optimizations. Performance depends on game scene, render resolution, CPU/GPU and streaming; no frame-rate guarantee.

Menus use sticks/buttons. No laser mouse, decorative menu rays, fake controllers or menu hands are included. The gameplay HUD curvature does not reshape the normal menu panels.

## Uninstall and recovery

Close the game, then run **Uninstall.cmd** from the extracted package. It removes files added by this installer and restores the originals it replaced. **If a previous VR mod was installed, uninstall restores that older mod.** Settings, saves, logs and backup files stay intact. Shared loader files remain when other plugin/patcher folders are detected.

Keep `ShipbreakerVR-InstallState` in the game directory: it contains the original backups and installation record. If setup is interrupted, run **Recover.cmd** before installing/uninstalling again. Do not manually delete that folder while the installation is active.

Setup verifies hashes before replacing/restoring files and stops on externally changed files or a conflicting shared loader. It supports normal physical folders, not linked/junction game paths. If access is denied, check folder permissions; setup does not automatically elevate.

**Check.cmd** performs a read-only package/game-folder preflight. Advanced/manual path selection:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Setup.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Hardspace Shipbreaker'
```

The scripts are unsigned. The ZIP SHA-256 detects accidental corruption; it is not a publisher signature. Obtain the package from a trusted source.

## Troubleshooting and status

See [known issues and test checklist](docs/KNOWN-ISSUES.md). Close the game before sharing `BepInEx/LogOutput.log`; review it for local paths/system details first. F7 records UI diagnostics. F9 starts/stops an optional timing capture; reports are written under `BepInEx`.

This beta adds couch/gamepad play and tool presentation improvements to the 0.4.32-beta1 baseline. The user accepted the final 0.4.44 gameplay test, including the heat-bar and tool-switch corrections. The package uses that accepted binary; the final ZIP is validated separately from headset gameplay. See docs/RELEASE-VALIDATION.md in the source repository. Other headset/runtime and Steam Input layout combinations remain unverified.

Source build instructions: [BUILD.md](docs/BUILD.md). Architecture and file inventory are in the source repository's `docs` folder. Mod source retains the upstream MIT license; bundled runtime notices are in [THIRD-PARTY.md](THIRD-PARTY.md) and `licenses`.
