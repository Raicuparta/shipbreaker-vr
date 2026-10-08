#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$StageDir,
    [Parameter(Mandatory=$true)][string]$UnityProjectDir,
    [Parameter(Mandatory=$true)][string]$OutputDir,
    [ValidatePattern('^beta[0-9]*$')][string]$PackageLabel = 'beta2'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$StageDir = (Resolve-Path -LiteralPath $StageDir).Path
$UnityProjectDir = (Resolve-Path -LiteralPath $UnityProjectDir).Path
$version = ([xml](Get-Content -LiteralPath "$repo/ShipbreakerVr/ShipbreakerVr.csproj" -Raw)).Project.PropertyGroup.Version
$dll = Join-Path $StageDir 'BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3) -ne $version) { throw 'Staged mod version does not match source.' }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path
$name = "ShipbreakerVR-$version-$PackageLabel-portable"
$package = Join-Path $OutputDir $name
$zip = $package + '.zip'
if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)) { throw 'Output already exists. Choose a new output folder; release artifacts are not overwritten.' }
New-Item -ItemType Directory -Path "$package/payload" -Force | Out-Null
function AddFile([string]$Source,[string]$Relative) {
    if (!(Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Missing package input: $Source" }
    $destination = Join-Path $package $Relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $destination
}
# Explicit groups: never include the whole build or a game directory.
foreach ($folder in @('BepInEx/core','BepInEx/plugins/ShipbreakerVr')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $StageDir $folder) -Filter '*.dll' -File) {
        if ($file.Name -match '^(Assembly-CSharp|BBI\.|UnityEngine\.|UnityEditor\.)') { throw "Game/editor assembly cannot be packaged: $($file.Name)" }
        AddFile $file.FullName ('payload/' + $folder + '/' + $file.Name)
    }
}
foreach ($file in @('xrmanager','debugrays')) { AddFile "$StageDir/BepInEx/plugins/ShipbreakerVr/AssetBundles/$file" "payload/BepInEx/plugins/ShipbreakerVr/AssetBundles/$file" }
AddFile "$StageDir/BepInEx/patchers/ShipbreakerVrPatcher/ShipbreakerVrPatcher.dll" 'payload/BepInEx/patchers/ShipbreakerVrPatcher/ShipbreakerVrPatcher.dll'
foreach ($file in @('winhttp.dll','doorstop_config.ini')) { AddFile "$StageDir/CopyToGame/$file" "payload/$file" }
$player = Join-Path $UnityProjectDir 'Build/ShipbreakerVrUnity_Data'
# Managed/native exports must come from one coherent dependency player.
foreach ($file in Get-ChildItem -LiteralPath "$package/payload/BepInEx/plugins/ShipbreakerVr" -Filter 'Unity*.dll' -File) {
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath "$player/Managed/$($file.Name)").Hash) { throw "Mixed Unity build exports: $($file.Name)" }
}
foreach ($file in @('Plugins/x86_64/UnityOpenXR.dll','Plugins/x86_64/openxr_loader.dll','UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json')) {
    AddFile "$player/$file" "payload/Shipbreaker_Data/$file"
    AddFile "$player/$file" "payload/BepInEx/patchers/ShipbreakerVrPatcher/CopyToGame/Shipbreaker_Data/$file"
}
# This is only seeded when absent. Existing shared BepInEx config is retained.
$bepConfig = @'
[Logging.Console]
Enabled = false

[Logging.Disk]
Enabled = true
WriteUnityLog = true
AppendLog = false
LogLevels = Fatal, Error, Warning, Message, Info
'@
New-Item -ItemType Directory -Force -Path "$package/payload/BepInEx/config" | Out-Null
$bepConfig | Set-Content -LiteralPath "$package/payload/BepInEx/config/BepInEx.cfg" -Encoding UTF8
AddFile "$PSScriptRoot/portable/Setup.ps1" 'Setup.ps1'
foreach ($action in @('Install','Uninstall','Check','Recover')) {
    $launcher = @"
@echo off
setlocal
cd /d "%~dp0"
echo Shipbreaker VR - $action
echo Close Shipbreaker before install, uninstall or recovery.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1" -Action $action
set "setupResult=%errorlevel%"
echo.
if not "%setupResult%"=="0" echo Setup did not complete. Read the message above.
pause
exit /b %setupResult%
"@
    [IO.File]::WriteAllText((Join-Path $package "$action.cmd"), ($launcher -replace '\r?\n',"`r`n"), [Text.Encoding]::ASCII)
}
foreach ($file in @('README.md','CHANGELOG.md','THIRD-PARTY.md','LICENSE','docs/CONTROLS.md','docs/FRAME-CONTROLS.md','docs/BUILD.md','docs/KNOWN-ISSUES.md')) { AddFile "$repo/$file" $file }
$requiredLicenses = @('BepInEx.txt','BepInEx.Harmony.txt','HarmonyX.txt','Harmony-original.txt','MonoMod.txt','Mono.Cecil.txt','Doorstop.txt','OpenXR-loader.txt','Unity-inputsystem.md','Unity-xrmanagement.md','Unity-openxr.md','Unity-subsystemregistration.md','Unity-openxr-ThirdParty.md')
foreach ($license in $requiredLicenses) { if (!(Test-Path -LiteralPath "$repo/licenses/$license")) { throw "Missing license: $license" } }
foreach ($file in Get-ChildItem -LiteralPath "$repo/licenses" -File) { AddFile $file.FullName ('licenses/' + $file.Name) }
& "$PSScriptRoot/Remove-BuildPaths.ps1" -Package $package
# Search binary bytes too: omitted PDB files do not remove embedded PDB paths.
foreach ($file in Get-ChildItem -LiteralPath $package -File -Recurse) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $ascii = [Text.Encoding]::UTF8.GetString($bytes)
    $wide = [Text.Encoding]::Unicode.GetString($bytes)
    foreach ($identifier in @($env:USERNAME,$env:COMPUTERNAME)) {
        if ($identifier -and $identifier.Length -ge 3 -and ($ascii.IndexOf($identifier,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or $wide.IndexOf($identifier,[StringComparison]::OrdinalIgnoreCase) -ge 0)) {
            throw "Personal build identifier remains in $($file.Name)."
        }
    }
}
$files = @(Get-ChildItem -LiteralPath "$package/payload" -File -Recurse | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName.Substring((Join-Path $package 'payload').Length + 1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
[pscustomobject]@{Schema=1;Version=$version;Files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$package/manifest.json" -Encoding UTF8
# Reviewable list covers scripts/docs as well as payload. It is not a signature.
Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash + '  ' + $_.FullName.Substring($package.Length + 1).Replace('\','/')
} | Set-Content -LiteralPath "$package/SHA256SUMS.txt" -Encoding UTF8
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash + '  ' + [IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII
Write-Output "Portable ZIP: $zip"
Write-Output "Payload files: $($files.Count)"
