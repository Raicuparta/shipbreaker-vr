# Portable 0.4.44-beta1 validation — 2026-10-07

- User accepted the final 0.4.44 gameplay test after confirming heat-bar hiding and then testing the ordinary tool-switch correction. Packaged the exact accepted test DLL (pre-sanitization SHA-256 `958057F526BBAB78A608D2431B07FB0DD4FA85F6BDB9943C0A9D926D49CC6B90`), without another gameplay or diagnostic change.
- Managed Release builds succeeded with zero warnings/errors; **448 checks passed**. The extra cases cover ordinary tool selection versus selected-but-stowed returns. Unity exports/packages and installer logic are unchanged; their earlier clean-restore/import and installer integration validation is retained, not represented as rerun.
- **21 full-package checks passed** under Windows PowerShell 5.1, including fresh/legacy install, reinstall, uninstall, payload checksums, preservation of unrelated data and accepted settings. A separate copy of the current recorded installation verified the private test-to-public transition: all 35 installed hashes matched and the personal mod configuration stayed byte-identical. The actual game installation was not changed by this preparation.
- Source and ZIP scan: 258 source files and 64 ZIP files, both compressed UnityFS bundles inspected, no checked private identifiers or credential patterns. Comparison to build inputs limits sanitization to debug-path regions in 12 unsigned DLLs; executable bytes remain unchanged. No personal mod config, game-managed DLLs, logs or test backups are distributed. This is a bounded scan, not proof against every possible encoding.
- ZIP: `ShipbreakerVR-0.4.44-beta1-portable.zip`; SHA-256 `A08E778DB2F688C66A8B57A9AB612E9ED53377E76873D993A0A96FD977CF2607`. Local publication records contain the final commit/tag and exact-commit audit. The final portable ZIP has not had a separate headset run, but its executable code is the accepted test code.

- Public documentation was revised after packaging. ZIP checksums were regenerated and verified; all 35 payload files and installer scripts are byte-identical to the package that passed the checks above. No gameplay or installer changes were made.

Historical test records follow.

# 0.4.44 tool-switch test — 2026-10-07

- User confirmed 0.4.43 hides the heat bar while the cutter is invisible, but reported delayed tool switching. The return gate had treated every unequipped tool as temporarily stowed, including tools not selected at all.
- Distinguish selected-but-stowed from unselected. Ordinary held-tool selection is visible immediately; a grab/interaction return still waits for the native pose to settle. Heat-bar visibility uses the same gate. Release build: zero warnings/errors; 448 managed checks passed, including ordinary switches, switching away during a pending return, and retaining protection when a hand remains occupied. Live verification remains pending.

# 0.4.43 heat-bar test — 2026-10-07

- Native `CutterHeatBarUIController` owns UI graphics separately from the tool mesh. The test build suppresses their render opacity through the same tool-return visibility path and restores native opacity at frame end/early update. No heat, input or interaction logic changes. Headset verification remains pending.
- Full .32-to-.42 payload comparison found only the main mod DLL and startup helper changed. Startup helper comparison found five identical method bodies; all packaged Unity/OpenXR dependencies and asset bundles are byte-identical.

# Portable 0.4.42-beta1 validation — 2026-10-07

- User accepted the final 0.4.41 gameplay test, including doors/consoles and tools returning without sliding up from the feet. Earlier iterations confirmed gamepad input, aiming reticles and shared 65% sizing. This is not an exhaustive test of every headset or Steam Input layout.
- Version 0.4.42 removes the temporary interaction trace and makes detailed gamepad diagnostics opt-in. A normalized IL comparison against the accepted 0.4.41 DLL found **688 identical methods**; the four added/removed/changed method entries are diagnostic-only. No gameplay method changed during release cleanup.
- Locked restores succeeded into new local NuGet caches using the verified offline feed. Both the working checkout and a separate 258-file source snapshot built Release with zero warnings/errors. The isolated build matched all **690 release method bodies**. The build wrapper now uses a single MSBuild node without compiler/node reuse; default reused workers failed restore in this environment.
- **441 managed checks passed**, covering tracking, controls, handoff, reticle geometry and tool-return gating. Unity source/packages/exports are unchanged from the verified baseline; no new Unity import was performed or needed for these managed-code changes.
- **33 installer integration checks** passed in a private restricted runner. Two junction-fixture checks could not run because the sandbox denied junction creation. The public test and installer junction-rejection code are unchanged, and these checks passed during prior release validation; they are not counted as rerun here.
- **21 full-package checks passed** in Windows PowerShell 5.1: extraction/checksums, fresh and legacy install/reinstall/uninstall, payload verification, settings/save preservation and restoration. A separate copy of the active recorded installation validated the local test-to-public transition and preserved accepted tool/HUD settings. No live game files were changed.
- Final ZIP: `ShipbreakerVR-0.4.42-beta1-portable.zip`; 35 payload files, 64 ZIP files. SHA-256: `CEC850419EC2DB08296DC37C070AC28ED9C6FC60260F54584E08C0708CCBB3E7`.
- Source and all ZIP files were scanned for local account/computer/workspace identifiers and selected credential patterns, including decompression of both UnityFS bundles. No checked matches remained. Independent comparison to build inputs limited payload sanitization to debug-path regions in 12 unsigned DLLs. No game-managed DLLs, personal mod config, logs or test backups are packaged. This is a bounded audit, not proof against every possible encoding.
- The release retains upstream attribution, MIT source licensing and bundled runtime notices. Source commit/tag and detailed audit records are recorded in the local publication handoff after committing. No remote push, tag publication, PR comment or release upload is part of this preparation.
- The final cleaned portable ZIP has not had a separate live installation/headset session. Other runtimes, the unconfirmed scanner stereo report and Steam Controller hardware remain outside this validation.

Historical validation follows.

# Portable 0.4.32-beta1 validation — 2026-10-01

- User accepted the 0.4.32 damage presentation after headset testing; input and repair were also confirmed during the preceding iterations. This is not a recorded pass of every regression on every supported headset.
- Built from a separate 252-file source snapshot. Locked NuGet restore into a new package cache from the previously verified offline feed succeeded. Release builds had zero warnings/errors; all **388 managed checks passed**. No new Unity import was necessary: Unity project, packages and exports are unchanged from the verified baseline.
- Packaged the accepted test binary. Independent byte comparison of every payload against its original input confirms that the only changes are CodeView directory-path removal in 12 unsigned DLLs. Executable code, resources and assembly identities are preserved.
- **35 installer integration checks** and **21 full-package checks** passed under Windows PowerShell 5.1, including extracted ZIP integrity, fresh install/reinstall/uninstall and legacy upgrade/restoration with settings retained.
- ZIP: `ShipbreakerVR-0.4.32-beta1-portable.zip`; 35 payload files, 64 ZIP files. SHA-256: `225EDF49B12B844BE06742F1C269CF4F2571C65DA0A46D0567E9A53AC8CD63F8`.
- All 252 source candidate files and all ZIP files were checked for local account/computer/workspace identifiers and selected credential patterns. Both UnityFS bundles were decompressed and checked. No matches remained. Runtime licences and upstream attribution are retained. This is a bounded audit, not a guarantee against every conceivable encoding.
- The public installer correctly rejects a local test DLL that differs from an active installation record. That protection is retained. Machine-specific transition tooling stays outside the public source/package.
- No live game files, remote branches, tags or release pages were changed by this preparation. The final ZIP has not had a separate live installation/headset run; the included executable code is the accepted test code. Other headsets and the unconfirmed scanner stereo report remain outside this validation.

Historical validation follows. Statements about older versions being unpublished describe their status at the time.

# Portable 0.4.27 beta validation

Prepared 2026-09-26. This is a packaging/diagnostic cleanup of the accepted 0.4.26 gameplay baseline. Tool alignment, HUD geometry, motion/menu bindings and haptics are unchanged. The additional-tool config description now distinguishes Pico triggers from Frame scanner bumpers.

## Build and artifact

- Release mod build: zero warnings/errors, using the pinned .NET SDK and the previously validated Unity 2020.3.17f1 dependency export.
- Managed regression suite: **320 checks passed**.
- ZIP: `ShipbreakerVR-0.4.27-beta-portable.zip`, 2,744,831 bytes.
- ZIP SHA-256: `AA0B8F2614AE47A4E12C944305E14B9EC3E1C763F717D12D23E2C7D28FABB850`.
- Plugin version: `0.4.27.0`.
- Plugin SHA-256: `072F019A27F1D742B788493BE36CAC4A3E834371BC2828FFC733F7BB6706B0A6`.
- 35 payload files; managed/native Unity exports verified against the same dependency player. No new Unity import/build was needed.

## Installer validation

Windows PowerShell 5.1 is used by the public CMD launchers and the tests.

- **35 isolated installer checks:** read-only preflight, paths with spaces, fresh install, repeat install, version update, section-aware obsolete-key cleanup, settings/save preservation, restoring a previous mod/native provider, conflicting shared loaders, modified package/installed files, traversal rejection, junction rejection, other-mod shared-loader retention, interrupted-operation recovery and automatic rollback after a copy failure.
- **18 full-package checks:** newly extracted ZIP checksums and content inspection; full 35-file fresh install/reinstall/uninstall; upgrade/reinstall/uninstall against a private fixture copied from the existing game installation; every owned file restored/removed and accepted HUD values retained.
- Steam registry/library auto-detection succeeded against the actual installation, without an explicit game path.
- Read-only package preflight passed against the live game. Its installed plugin remained `0.4.26.0`; no live installation was performed during release preparation.
- Initial package inspection found no game executable/assemblies, PDB files, personal mod configuration, logs, captured timings or backup folders. The initial path scan covered text files only and missed embedded CodeView paths in DLLs; see the correction below. Runtime license files and notices are included.

## Remaining hardware validation

The user reported accepted Pico gameplay, Frame native controls and Frame full-grip shortcuts in earlier builds. This does not constitute a headset test of the final portable 0.4.27 package. Run the checklist in `KNOWN-ISSUES.md` after installing it. Steam detection was exercised; the folder picker was not manually clicked during automated checks.

The package is an unsigned beta ZIP with integrity hashes, not an authenticated publisher signature. It has not been uploaded or published. New machines/headsets, protected-folder permissions and future game/runtime versions remain outside the local validation matrix.

## Hardware report and privacy correction — beta2

The user subsequently reported that the portable 0.4.27 build works fine. This is an overall acceptance report, not a recorded pass for every individual checklist item.

A binary privacy audit found the development username/workspace in CodeView PDB paths embedded in 12 locally compiled DLLs, including Unity managed exports. Omitting the PDB files had not removed those strings. The original beta ZIP should not be shared as anonymous.

`ShipbreakerVR-0.4.27-beta2-portable.zip` replaces that shareable artifact. SHA-256: `A572BAD99F29654B53A159F501CDBAE7D77FF08DDE26914FD2B3DCC7898160CA`.

- Debug path strings were reduced to bare PDB filenames, with the unused bytes zeroed. The sanitizer refuses signed assemblies.
- Independent byte comparison confirms that all payload changes are confined to those debug path regions. Code, resources and assembly versions remain identical to the tested build.
- All 63 ZIP files were scanned as bytes for the local username, computer name and workspace identifier. Both UnityFS asset bundles were decompressed and scanned too. No matches remained.
- All 18 full-package extraction/integrity/install/update/uninstall checks passed again, including upgrade from the installed portable version.
- Upstream authors' license notices and generic Pico/Frame compatibility names remain intentionally included. This is a bounded identifier/content audit, not a guarantee against every conceivable identifier encoding.
- Future packaging runs now strip those paths and scan binary contents for local username/computer-name strings. `PackagePrivacyChecks.py` provides the independent compressed-asset and byte-change audit.

The mod source retains its MIT license locally; the updated source has not yet been publicly published. Unity runtime packages retain their own license terms.

## Fresh source verification — 2026-09-27

- Copied the 246 Git candidate files to an isolated source directory and verified their SHA-256 hashes against the working source. The Unity project used a separate short path and started without `Library`, `Temp` or generated build output.
- Downloaded the two pinned .NET reference packages anew from the official NuGet endpoint. Locked restores of the mod, patcher and managed tests succeeded into a new package cache; content hashes matched all lockfiles. The sandbox's Windows TLS client could not connect, so an HTTPS download client supplied a local feed to NuGet. Direct online restore from that sandbox remains unverified.
- Unity **2020.3.17f1** completed the clean import, dependency-player build and debug material bundle build through the interactive editor. Batch mode rejected the existing licence before import, including outside the sandbox; the documented interactive fallback succeeded.
- Unity package manifest, package lockfile, editor version and build-method source remained unchanged by the import. This verifies a clean project import, not an empty global Unity package cache or byte-identical shader exports.
- Built the mod and patcher against the newly generated Unity exports: **zero warnings and zero errors**. The freshly restored managed suite compiled successfully and **all 320 checks passed**.
- This verification did not install the regenerated build or replace the accepted beta2 portable ZIP. A fresh source build is not an additional headset test.
