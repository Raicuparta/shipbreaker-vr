# Build and package

Use a short physical checkout path (for example `C:\src\shipbreaker-vr`) and a short Unity editor path. Unity 2020 import/shader tools can fail with deeply nested Windows paths. Keep the game and Unity closed while staging/installing.

## Inputs

- Windows; installed Steam Mono build of Hardspace: Shipbreaker (game DLLs are references only).
- .NET SDK **8.0.408**, pinned in `global.json`; framework reference packages locked by `packages.lock.json`.
- Unity **2020.3.17f1**, Windows build support and a valid editor licence.
- Committed Unity package manifest/lock: OpenXR **1.2.8**, Input System **1.0.2**, XR Management **4.2.1**, HDRP **10.6.0**. Do not upgrade the editor/packages as part of an ordinary mod build.

```powershell
.\scripts\build.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Hardspace Shipbreaker' -UnityEditor 'C:\Unity2020\Editor\Unity.exe'
```

This builds the dependency player/debug material, restores locked packages, builds Release and stages `artifacts/Release/Mod`. It does not install anything. `-DotNet` selects a local SDK executable. `-UnityProjectDir` selects a short copy of `ShipbreakerVrUnity` if necessary; resync source assets/packages/settings before rebuilding that copy.

For an offline package feed, `-NuGetSource` selects a folder containing the pinned `.nupkg` files. Locked restore still verifies their content hashes. The ordinary build uses NuGet.org.

If batch licensing fails but the licensed interactive editor works, open the Unity dependency project and choose **Shipbreaker VR > Build dependencies and debug material**. After success, close Unity and finish with:

```powershell
.\scripts\build.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Hardspace Shipbreaker' -SkipUnity
```

If the editor project is at a separate short path, also pass `-UnityProjectDir` with that path. Only use `-SkipUnity` when the pinned dependencies/editor assets have not changed. Keep Unity exports together: managed DLLs, native OpenXR provider/loader, subsystem manifest and bundles. The generated `build-inputs.json` records reference/export hashes. Matching these inputs gives a repeatable managed build; this is not a claim that separate Unity shader builds are byte-identical.

## Tests

```powershell
dotnet restore tests/TrackingChecks.csproj --locked-mode --configfile NuGet.Config
dotnet build tests/TrackingChecks.csproj --no-restore -c Release '-p:GameDir=D:\SteamLibrary\steamapps\common\Hardspace Shipbreaker'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Prepare-InControlFixture.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Hardspace Shipbreaker'
.\tests\bin\Release\net48\TrackingChecks.exe
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\PortableInstallerChecks.ps1
```

The managed suite covers 448 tracking/input/geometry/cache checks. Preparing the private InControl fixture replaces only its Unity-native clock call so desktop tests can exercise input commit behavior; installed game files are not edited. The installer suite uses an isolated fixture and Windows PowerShell 5.1, matching the public CMD launchers. Neither simulates headset rendering or Unity lifecycle behavior.

## Portable ZIP

```powershell
.\scripts\package.ps1 -StageDir .\artifacts\Release\Mod -UnityProjectDir .\ShipbreakerVrUnity -OutputDir .\artifacts\packages -PackageLabel beta1
```

Packaging uses explicit file groups, re-exports native files from the matching dependency player, includes runtime licenses and hashes every payload file. It excludes game DLLs, Unity player/editor binaries, PDBs, local configs, logs, performance captures and test backups. It creates the ZIP and an adjacent SHA-256 file; these are not cryptographic signatures.

Use `Check.cmd` against the intended game folder before install. Run [the final headset checklist](KNOWN-ISSUES.md) after installing the package. Preserve release hashes and results in `RELEASE-VALIDATION.md`; do not mark pending hardware checks passed based only on compilation.

Developer architecture/inventory documents remain in this source checkout. Historical trial instructions were moved under `docs/history` and are not included in the portable package. The old machine-specific launchers outside the repository are not part of this installer.
