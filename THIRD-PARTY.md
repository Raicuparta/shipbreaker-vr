# Third-party software

Shipbreaker VR is based on [Raicuparta/shipbreaker-vr](https://github.com/Raicuparta/shipbreaker-vr). Its upstream MIT notice is retained in `LICENSE`. No proprietary Shipbreaker executable, managed game assembly, extracted suit/tool mesh or game texture is distributed. Game visuals are used from the user's installed game at runtime.

| Distributed component | Version / source | Notice |
| --- | --- | --- |
| BepInEx core/preloader | 5.4.21, [source](https://github.com/BepInEx/BepInEx/tree/v5.4.21) | `licenses/BepInEx.txt` (MIT) |
| BepInEx.Harmony, HarmonyXInterop, 0Harmony20 | upstream BepInEx.Harmony compatibility set, [source](https://github.com/BepInEx/BepInEx.Harmony) | `licenses/BepInEx.Harmony.txt`, Harmony notices (MIT) |
| HarmonyX / 0Harmony | 2.9.0, [source](https://github.com/BepInEx/HarmonyX/tree/v2.9.0) | `licenses/HarmonyX.txt`, `licenses/Harmony-original.txt` (MIT) |
| MonoMod.RuntimeDetour / Utils | 22.1.29.1, [source](https://github.com/MonoMod/MonoMod/tree/v22.01.29.01) | `licenses/MonoMod.txt` (MIT) |
| Mono.Cecil and symbols/readers | 0.10.4, [source](https://github.com/jbevain/cecil/tree/0.10.4) | `licenses/Mono.Cecil.txt` (MIT) |
| Unity Doorstop / winhttp | 3.4.0, [legacy source](https://github.com/NeighTools/UnityDoorstop/tree/legacy) | `licenses/Doorstop.txt` (CC0 1.0) |
| Unity Input System | 1.0.2 | `licenses/Unity-inputsystem.md` and companion third-party notices |
| Unity XR Management | 4.2.1 | `licenses/Unity-xrmanagement.md` |
| Unity OpenXR managed/native exports | 1.2.8 | `licenses/Unity-openxr.md`, `licenses/Unity-openxr-ThirdParty.md` |
| Unity Subsystem Registration | 1.0.6 | `licenses/Unity-subsystemregistration.md` |
| Khronos OpenXR loader/SDK portions | supplied with the pinned Unity OpenXR Windows export | `licenses/OpenXR-loader.txt` (Apache 2.0), OpenXR third-party notice |

Unity packages have their own Unity license terms; they are not relicensed under this mod's MIT license. The original package license/notice files are included verbatim. Windows runtime exports are shipped for use with this Unity game; the sample Unity player/editor is not shipped. Android/Oculus mobile native runtime files are not included.

Dependency source links and license files describe the versions bundled here, not the licenses of newer major releases. In particular this package uses legacy Doorstop 3 and BepInEx 5.4.21.

The source checkout also includes Unity's editor-only [Asset Bundle Browser](https://github.com/Unity-Technologies/AssetBundles-Browser) under `ShipbreakerVrUnity/Assets/AssetBundlesBrowser`. Its [Unity Companion License notice](licenses/Unity-assetbundlebrowser.md) applies to that code. The browser is not part of the portable runtime payload.
