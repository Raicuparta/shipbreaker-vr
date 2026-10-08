# Upstream file-by-file inventory

Baseline commit: `0c13a9997b9725d699092688d51277f4bc9b0211`. **149 tracked files** inventoried from Git, including files replaced by this candidate.

All mod/patcher runtime C# was reviewed in full for this architecture map. Serialized settings, manifests, project/package files and vendor editor code were read/indexed for their role and dependencies. Unchanged third-party editor code and bundled native libraries were not subjected to a comprehensive correctness/security audit. Binary payloads were inventoried and hashed rather than treated as readable source. `.meta` files are listed individually because their GUIDs link settings/assets to package scripts.

Full SHA-256 hashes, sizes, line counts and editor/runtime C# type declarations are in [upstream-inventory.json](upstream-inventory.json). These refer to the upstream commit, not modified candidate files. See [ARCHITECTURE.md](ARCHITECTURE.md) for the current runtime design and game-symbol evidence.

| Upstream file | Size | Inspected role |
|---|---:|---|
| `.gitignore` | 7 lines | Original build/IDE exclusions; expanded for Unity caches and generated output. |
| `Directory.Build.props` | 9 lines | Original author-specific RaiManager output paths; replaced by repository-local staging. |
| `LICENSE` | 21 lines | MIT licence for repository code; third-party binary/package licences remain separate. |
| `README.md` | 34 lines | Upstream install/play instructions, F3/F5/F6, explicit absence of motion controls. |
| `ShipbreakerVr.sln` | 22 lines | Two-project Debug/Release solution; no Unity editor build automation upstream. |
| `ShipbreakerVr/.gitignore` | 1 lines | Repository support asset; retained upstream. |
| `ShipbreakerVr/ModFiles/BepInEx/config/BepInEx.cfg` | 151 lines | BepInEx cache, console, disk logging and preloader configuration. |
| `ShipbreakerVr/ModFiles/BepInEx/core/0Harmony.dll` | 204,800 bytes | Bundled loader/patching dependency: 0Harmony.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/0Harmony.xml` | 5252 lines | API documentation for bundled dependency: 0Harmony.xml. |
| `ShipbreakerVr/ModFiles/BepInEx/core/0Harmony20.dll` | 111,616 bytes | Bundled loader/patching dependency: 0Harmony20.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/BepInEx.Harmony.dll` | 5,632 bytes | Bundled loader/patching dependency: BepInEx.Harmony.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/BepInEx.Harmony.xml` | 86 lines | API documentation for bundled dependency: BepInEx.Harmony.xml. |
| `ShipbreakerVr/ModFiles/BepInEx/core/BepInEx.Preloader.dll` | 42,496 bytes | Bundled loader/patching dependency: BepInEx.Preloader.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/BepInEx.Preloader.xml` | 176 lines | API documentation for bundled dependency: BepInEx.Preloader.xml. |
| `ShipbreakerVr/ModFiles/BepInEx/core/BepInEx.dll` | 125,440 bytes | Bundled loader/patching dependency: BepInEx.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/BepInEx.xml` | 1869 lines | API documentation for bundled dependency: BepInEx.xml. |
| `ShipbreakerVr/ModFiles/BepInEx/core/HarmonyXInterop.dll` | 23,552 bytes | Bundled loader/patching dependency: HarmonyXInterop.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/Mono.Cecil.Mdb.dll` | 43,008 bytes | Bundled loader/patching dependency: Mono.Cecil.Mdb.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/Mono.Cecil.Pdb.dll` | 86,528 bytes | Bundled loader/patching dependency: Mono.Cecil.Pdb.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/Mono.Cecil.Rocks.dll` | 27,648 bytes | Bundled loader/patching dependency: Mono.Cecil.Rocks.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/Mono.Cecil.dll` | 339,456 bytes | Bundled loader/patching dependency: Mono.Cecil.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/MonoMod.RuntimeDetour.dll` | 105,984 bytes | Bundled loader/patching dependency: MonoMod.RuntimeDetour.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/MonoMod.RuntimeDetour.xml` | 189 lines | API documentation for bundled dependency: MonoMod.RuntimeDetour.xml. |
| `ShipbreakerVr/ModFiles/BepInEx/core/MonoMod.Utils.dll` | 187,904 bytes | Bundled loader/patching dependency: MonoMod.Utils.dll. |
| `ShipbreakerVr/ModFiles/BepInEx/core/MonoMod.Utils.xml` | 1549 lines | API documentation for bundled dependency: MonoMod.Utils.xml. |
| `ShipbreakerVr/ModFiles/CopyToGame/doorstop_config.ini` | 16 lines | Doorstop boot configuration and BepInEx preloader entrypoint. |
| `ShipbreakerVr/ModFiles/CopyToGame/winhttp.dll` | 25,088 bytes | Windows native Doorstop bootstrap binary, not tool/gameplay logic. |
| `ShipbreakerVr/ModFiles/icon.png` | 11,484 bytes | Mod branding/icon image. |
| `ShipbreakerVr/ModFiles/manifest.json` | 19 lines | RaiManager game/mod identifiers and Steam/Xbox installation hints. |
| `ShipbreakerVr/ModXrManager.cs` | 63 lines | F3; XR settings bundle; XR loader startup/shutdown; original private-state reflection. |
| `ShipbreakerVr/NuGet.Config` | 6 lines | Original BepInEx package feed; superseded by root configuration and vendored loader references. |
| `ShipbreakerVr/Patches.cs` | 34 lines | Only two Harmony postfixes: LynxCameraController.Start and CanvasScaler.OnEnable. |
| `ShipbreakerVr/ShipbreakerVr.csproj` | 85 lines | Original net48 plugin, game/BepInEx packages, uncommitted Unity-export references, bundle copying. |
| `ShipbreakerVr/ShipbreakerVrMod.cs` | 53 lines | BepInEx entry point; Harmony registration; F5 quality and F6 lights. |
| `ShipbreakerVr/TypeExtensions.cs` | 28 lines | Reflection helper searching up to two base types; formerly used for loader state. |
| `ShipbreakerVr/VrAssetManager.cs` | 23 lines | Loads plugin-relative AssetBundles from the BepInEx plugin folder. |
| `ShipbreakerVr/VrCamera.cs` | 34 lines | Child stereo camera, copied clip/mask settings, rotation-only TrackedPoseDriver, .2m eye offset. |
| `ShipbreakerVr/VrUi.cs` | 65 lines | World-space root canvases, .001 scale, 1.5m distance, saved/restored layout and scaler toggles. |
| `ShipbreakerVrPatcher/CopyToGame/Shipbreaker_Data/Plugins/x86_64/UnityOpenXR.dll` | 581,632 bytes | Native OpenXR runtime provider payload. |
| `ShipbreakerVrPatcher/CopyToGame/Shipbreaker_Data/Plugins/x86_64/openxr_loader.dll` | 1,860,096 bytes | Native OpenXR runtime provider payload. |
| `ShipbreakerVrPatcher/CopyToGame/Shipbreaker_Data/UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json` | 15 lines | Unity subsystem discovery manifest (OpenXR display/input provider). |
| `ShipbreakerVrPatcher/Patcher.cs` | 65 lines | BepInEx preload initialization copies native provider files; assembly Patch method is empty. |
| `ShipbreakerVrPatcher/Properties/AssemblyInfo.cs` | 35 lines | Patcher .NET assembly identity/version attributes. |
| `ShipbreakerVrPatcher/ShipbreakerVrPatcher.csproj` | 30 lines | Original net35 preloader, Cecil reference and CopyToGame payload. |
| `ShipbreakerVrUnity/.gitignore` | 72 lines | Repository support asset; retained upstream. |
| `ShipbreakerVrUnity/AssetBundles/AssetBundles` | 1,020 bytes | AssetBundle build manifest/hash metadata. |
| `ShipbreakerVrUnity/AssetBundles/AssetBundles.manifest` | 7 lines | AssetBundle build manifest/hash metadata. |
| `ShipbreakerVrUnity/AssetBundles/xrmanager` | 3,566 bytes | Serialized XR settings bundle. |
| `ShipbreakerVrUnity/AssetBundles/xrmanager.manifest` | 53 lines | AssetBundle build manifest/hash metadata. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser.meta` | 8 lines | Unity GUID/import metadata for AssetBundlesBrowser; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor.meta` | 8 lines | Unity GUID/import metadata for Editor; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleBrowserMain.cs` | 251 lines | Vendored editor only: Editor window and tabs. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleBrowserMain.cs.meta` | 11 lines | Unity GUID/import metadata for AssetBundleBrowserMain.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleBuildTab.cs` | 461 lines | Vendored editor only: Interactive bundle build UI. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleBuildTab.cs.meta` | 11 lines | Unity GUID/import metadata for AssetBundleBuildTab.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleDataSource.meta` | 9 lines | Unity GUID/import metadata for AssetBundleDataSource; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleDataSource/ABDataSource.cs` | 126 lines | Vendored editor only: Data-source contract. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleDataSource/ABDataSource.cs.meta` | 11 lines | Unity GUID/import metadata for ABDataSource.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleDataSource/ABDataSourceProvider.cs` | 55 lines | Vendored editor only: Data-source discovery. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleDataSource/ABDataSourceProvider.cs.meta` | 11 lines | Unity GUID/import metadata for ABDataSourceProvider.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleDataSource/AssetDatabaseABDataSource.cs` | 104 lines | Vendored editor only: Unity AssetDatabase adapter. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleDataSource/AssetDatabaseABDataSource.cs.meta` | 11 lines | Unity GUID/import metadata for AssetDatabaseABDataSource.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleManageTab.cs` | 277 lines | Vendored editor only: Interactive bundle assignment UI. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleManageTab.cs.meta` | 11 lines | Unity GUID/import metadata for AssetBundleManageTab.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleModel.meta` | 9 lines | Unity GUID/import metadata for AssetBundleModel; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleModel/ABModel.cs` | 797 lines | Vendored editor only: Bundle browser model. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleModel/ABModel.cs.meta` | 11 lines | Unity GUID/import metadata for ABModel.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleModel/ABModelAssetInfo.cs` | 246 lines | Vendored editor only: Per-asset dependency model. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleModel/ABModelAssetInfo.cs.meta` | 11 lines | Unity GUID/import metadata for ABModelAssetInfo.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleModel/ABModelBundleInfo.cs` | 1042 lines | Vendored editor only: Per-bundle model. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleModel/ABModelBundleInfo.cs.meta` | 11 lines | Unity GUID/import metadata for ABModelBundleInfo.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleTree.cs` | 647 lines | Vendored editor only: Bundle tree view. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetBundleTree.cs.meta` | 11 lines | Unity GUID/import metadata for AssetBundleTree.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetListTree.cs` | 475 lines | Vendored editor only: Asset list view. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/AssetListTree.cs.meta` | 11 lines | Unity GUID/import metadata for AssetListTree.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/BundleDetailList.cs` | 321 lines | Vendored editor only: Bundle details view. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/BundleDetailList.cs.meta` | 11 lines | Unity GUID/import metadata for BundleDetailList.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/Icons.meta` | 9 lines | Unity GUID/import metadata for Icons; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/Icons/ABundleBrowserIconY1756Basic.png` | 1,499 bytes | Vendored AssetBundle Browser editor icon/resource. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/Icons/ABundleBrowserIconY1756Basic.png.meta` | 77 lines | Unity GUID/import metadata for ABundleBrowserIconY1756Basic.png; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/Icons/ABundleBrowserIconY1756Scene.png` | 1,617 bytes | Vendored AssetBundle Browser editor icon/resource. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/Icons/ABundleBrowserIconY1756Scene.png.meta` | 77 lines | Unity GUID/import metadata for ABundleBrowserIconY1756Scene.png; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab.meta` | 9 lines | Unity GUID/import metadata for InspectTab; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/AssetBundleInspectTab.cs` | 514 lines | Vendored editor only: Bundle inspection UI. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/AssetBundleInspectTab.cs.meta` | 11 lines | Unity GUID/import metadata for AssetBundleInspectTab.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/AssetBundleRecord.cs` | 60 lines | Vendored editor only: Inspection record persistence. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/AssetBundleRecord.cs.meta` | 11 lines | Unity GUID/import metadata for AssetBundleRecord.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/InspectSingleBundle.cs` | 129 lines | Vendored editor only: Single bundle inspection. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/InspectSingleBundle.cs.meta` | 11 lines | Unity GUID/import metadata for InspectSingleBundle.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/InspectTreeView.cs` | 137 lines | Vendored editor only: Inspector tree UI. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/InspectTab/InspectTreeView.cs.meta` | 11 lines | Unity GUID/import metadata for InspectTreeView.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/MessageList.cs` | 116 lines | Vendored editor only: Editor message list. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/MessageList.cs.meta` | 11 lines | Unity GUID/import metadata for MessageList.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/MessageSystem.cs` | 198 lines | Vendored editor only: Editor message severity/types. No in-game patches. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/MessageSystem.cs.meta` | 11 lines | Unity GUID/import metadata for MessageSystem.cs; preserve identity. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/Unity.AssetBundleBrowser.Editor.asmdef` | 9 lines | Editor-only assembly boundary for vendored AssetBundle Browser. |
| `ShipbreakerVrUnity/Assets/AssetBundlesBrowser/Editor/Unity.AssetBundleBrowser.Editor.asmdef.meta` | 7 lines | Unity GUID/import metadata for Unity.AssetBundleBrowser.Editor.asmdef; preserve identity. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources.meta` | 8 lines | Unity GUID/import metadata for HDRPDefaultResources; preserve identity. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultHDRISky.exr` | 188,717 bytes | Dependency-project HDRP default render asset/resource; not a game HUD or tool adapter. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultHDRISky.exr.meta` | 106 lines | Unity GUID/import metadata for DefaultHDRISky.exr; preserve identity. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultHDRPAsset.asset` | 451 lines | Dependency-project HDRP default render asset/resource; not a game HUD or tool adapter. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultHDRPAsset.asset.meta` | 8 lines | Unity GUID/import metadata for DefaultHDRPAsset.asset; preserve identity. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultSceneRoot.prefab` | 516 lines | Dependency-project HDRP default render asset/resource; not a game HUD or tool adapter. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultSceneRoot.prefab.meta` | 7 lines | Unity GUID/import metadata for DefaultSceneRoot.prefab; preserve identity. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultVFXResources.asset` | 112 lines | Dependency-project HDRP default render asset/resource; not a game HUD or tool adapter. |
| `ShipbreakerVrUnity/Assets/HDRPDefaultResources/DefaultVFXResources.asset.meta` | 8 lines | Unity GUID/import metadata for DefaultVFXResources.asset; preserve identity. |
| `ShipbreakerVrUnity/Assets/Scene.unity` | 749 lines | Dependency-export Unity scene; not a Shipbreaker game scene. |
| `ShipbreakerVrUnity/Assets/Scene.unity.meta` | 7 lines | Unity GUID/import metadata for Scene.unity; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR.meta` | 8 lines | Unity GUID/import metadata for XR; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR/Loaders.meta` | 8 lines | Unity GUID/import metadata for Loaders; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR/Loaders/Open XR Loader No Pre Init.asset` | 14 lines | Serialized alternate no-pre-init OpenXR loader asset. |
| `ShipbreakerVrUnity/Assets/XR/Loaders/Open XR Loader No Pre Init.asset.meta` | 8 lines | Unity GUID/import metadata for Open XR Loader No Pre Init.asset; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR/Loaders/Open XR Loader.asset` | 14 lines | Serialized OpenXR loader asset. |
| `ShipbreakerVrUnity/Assets/XR/Loaders/Open XR Loader.asset.meta` | 8 lines | Unity GUID/import metadata for Open XR Loader.asset; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR/Settings.meta` | 8 lines | Unity GUID/import metadata for Settings; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR/Settings/Open XR Package Settings.asset` | 266 lines | OpenXR feature subassets; Touch on, other standard controllers off upstream; render mode 0. |
| `ShipbreakerVrUnity/Assets/XR/Settings/Open XR Package Settings.asset.meta` | 8 lines | Unity GUID/import metadata for Open XR Package Settings.asset; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR/Settings/OpenXR Editor Settings.asset` | 16 lines | Editor-side OpenXR configuration included in xrmanager bundle. |
| `ShipbreakerVrUnity/Assets/XR/Settings/OpenXR Editor Settings.asset.meta` | 8 lines | Unity GUID/import metadata for OpenXR Editor Settings.asset; preserve identity. |
| `ShipbreakerVrUnity/Assets/XR/XRGeneralSettings.asset` | 48 lines | Standalone XRGeneralSettings, manager and serialized OpenXR loader list. |
| `ShipbreakerVrUnity/Assets/XR/XRGeneralSettings.asset.meta` | 8 lines | Unity GUID/import metadata for XRGeneralSettings.asset; preserve identity. |
| `ShipbreakerVrUnity/Packages/manifest.json` | 48 lines | Direct Unity package pins, including OpenXR 1.2.8/Input System 1.0.2/HDRP 10.6.0. |
| `ShipbreakerVrUnity/Packages/packages-lock.json` | 459 lines | Resolved transitive package versions, sources and dependency graph. |
| `ShipbreakerVrUnity/ProjectSettings/AudioManager.asset` | 19 lines | Unity project settings: AudioManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/ClusterInputManager.asset` | 6 lines | Unity project settings: ClusterInputManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/DynamicsManager.asset` | 30 lines | Unity project settings: DynamicsManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/EditorBuildSettings.asset` | 14 lines | Unity project settings: EditorBuildSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/EditorSettings.asset` | 37 lines | Unity project settings: EditorSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/GraphicsSettings.asset` | 134 lines | Unity project settings: GraphicsSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/HDRPProjectSettings.asset` | 22 lines | Unity project settings: HDRPProjectSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/InputManager.asset` | 487 lines | Unity project settings: InputManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/NavMeshAreas.asset` | 91 lines | Unity project settings: NavMeshAreas. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/NetworkManager.asset` | 8 lines | Unity project settings: NetworkManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/PackageManagerSettings.asset` | 45 lines | Unity project settings: PackageManagerSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/Physics2DSettings.asset` | 56 lines | Unity project settings: Physics2DSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/PresetManager.asset` | 13 lines | Unity project settings: PresetManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/ProjectSettings.asset` | 705 lines | Player build/API/platform settings; new Input System selected in dependency project. |
| `ShipbreakerVrUnity/ProjectSettings/ProjectVersion.txt` | 2 lines | Pinned Unity editor 2020.3.17f1 / a4537701e4ab. |
| `ShipbreakerVrUnity/ProjectSettings/QualitySettings.asset` | 121 lines | Unity project settings: QualitySettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/TagManager.asset` | 43 lines | Unity project settings: TagManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/TimeManager.asset` | 9 lines | Unity project settings: TimeManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/TimelineSettings.asset` | 15 lines | Unity project settings: TimelineSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/URPProjectSettings.asset` | 15 lines | Unity project settings: URPProjectSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/UnityConnectSettings.asset` | 35 lines | Unity project settings: UnityConnectSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/VFXManager.asset` | 14 lines | Unity project settings: VFXManager. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/VersionControlSettings.asset` | 8 lines | Unity project settings: VersionControlSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/XRPackageSettings.asset` | 5 lines | Unity project settings: XRPackageSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/ProjectSettings/XRSettings.asset` | 10 lines | Unity project settings: XRSettings. Retained without an engine upgrade. |
| `ShipbreakerVrUnity/unityProject.vrmanifest` | 16 lines | Legacy editor VR application metadata; not the active game OpenXR bootstrap. |

## Files added for candidate 0.4.7

| File | Responsibility |
| --- | --- |
| `ShipbreakerVr/Tracking/ToolProjection.cs` | Reusable normalized pixel-to-angular-ray math, independent of camera pixel rect. |
| `ShipbreakerVr/Tracking/VrToggleGesture.cs` | Continuous hold, centered-stick checks, release/rearm and tracking/focus cancellation. |
| `ShipbreakerVr/Tracking/ArmGeometry.cs` | Bounded two-bone geometry that preserves upper/lower arm lengths. |
| `ShipbreakerVr/DemoChargeAimPatches.cs` | Scope VR throws and apply captured direction to only their new entities before native initialization. |
| `ShipbreakerVr/VrDemoReticle.cs` | Local HUD-plane rotation while VR is enabled and native rotation restoration. |
| `ShipbreakerVr/ScreenDimensionsPatch.cs` | Refresh native cached dimensions when switching between desktop and XR views. |
| `ShipbreakerVr/VrAvatarVisuals.cs` | Exact native arm/tool registration, temporary VR render fitting, restoration, and size/rig diagnostics. |

Changed adapters: `VrAdditionalToolControls` and `AdditionalToolPatches` use the
projection helper; `VrSplitsawPreview` uses the same ray geometry as cuts;
`ModXrManager` separates session and view lifetime, starts automatically and reads
the hold gesture; `VrMenuControls` reserves the assembled gesture;
`VrCameraSubmission` preserves mono desktop rendering with a warm XR session;
`VrCamera` exposes eye offset and `VrUi` preserves 1.3 m panel distance;
`ShipbreakerVrMod` registers configuration/components. Tests link only reusable
pure logic, not game assets. Proprietary decompilation remains outside the repo.

0.4.8 additions: `HabNavigationUiPatch.cs` owns only normalized habitat prompt
projection; `VrHeadVisibility.cs` owns temporary head/helmet visual suppression and
explicit HelmetController registration. `VrAvatarVisuals.cs` adds Animation Rigging/
humanoid matching, early restoration, pre-skinning posing and mesh-bound updates.


0.4.9 replaces the earlier avatar fitting: no active ArmGeometry/TwoBoneIK/humanoid
posing remains. `VrAvatarVisuals` now suppresses suit/arms and places disembodied tools;
`VrHeadVisibility` no longer scales head bones. New files:

| File | Responsibility |
| --- | --- |
| `ShipbreakerVr/VrGloveVisuals.cs` | Cache isolated native glove geometry/materials at runtime; tracked grip placement without a skeleton. |
| `ShipbreakerVr/VrToolPresentation.cs` | Native live range/mask and lifecycle adapters, contextual guide settings. |
| `ShipbreakerVr/VrHudCurve.cs` | Verified UI/TMP mesh capture, shallow helmet geometry curve and restoration. |
| `ShipbreakerVr/Tracking/PresentationGeometry.cs` | Pure hand-frame, HUD-depth and range-limit math, covered by managed checks. |

`ControllerDebugRays`, `VrUi`, configuration/version and tests also change. Cached game
meshes remain in process only. Historical inventory entries describe earlier versions.


0.4.10: `HudMeshPatchTargets.cs` centralizes exact declared method/signature resolution
and is linked into regression tests against installed UI/TMP assemblies. The plugin
entrypoint isolates optional HUD patch initialization from core components.


## 0.4.42 additions

| File | Responsibility |
| --- | --- |
| `ShipbreakerVr/VrInputMode.cs` | Input ownership, native gamepad sampling, handoff cancellation/neutral gating and optional diagnostics. |
| `ShipbreakerVr/NativeGamepadRouting.cs` | Preserve native InControl arbitration after the neutral gate. |
| `ShipbreakerVr/Tracking/InputModeSelection.cs` | Reusable activity thresholds and automatic/fixed source selection. |
| `ShipbreakerVr/VrCouchReticle.cs` | World-space UI aiming ring without input/raycast components. |
| `ShipbreakerVr/Tracking/ToolReturnVisibility.cs` | Reusable presentation-only stow/settle/visible state. |
| `tests/InputModeChecks.cs` | Input arbitration, stale/held activity, handoff and device-selection regression checks. |

Tool adapters, haptics and menu ownership honor couch mode. Avatar/held-prop presentation uses shared scaling and stow visibility. Existing tests also cover reticle clipping/bearing and repeated tool return. Temporary interaction diagnostic code is excluded from the release.
