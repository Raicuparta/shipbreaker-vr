[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [string]$UnityEditor,
    [string]$UnityProjectDir,
    [string]$DotNet = 'dotnet',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$SkipUnity,
    [string]$NuGetSource
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$UnityProjectDir) { $UnityProjectDir = Join-Path $repoRoot 'ShipbreakerVrUnity' }
$UnityProjectDir = (Resolve-Path -LiteralPath $UnityProjectDir).Path
if (!(Test-Path -LiteralPath "$UnityProjectDir/Assets/Editor/MilestoneBuild.cs")) { throw 'UnityProjectDir must contain the milestone dependency project.' }
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
if (!(Test-Path -LiteralPath "$GameDir/Shipbreaker.exe")) { throw 'GameDir must contain Shipbreaker.exe.' }
if (!(Test-Path -LiteralPath "$GameDir/Shipbreaker_Data/Managed/BBI.Unity.Game.dll")) { throw 'The Mono game assemblies are missing.' }
$artifacts = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
Push-Location $repoRoot
try {
    if (!$SkipUnity) {
        if (!$UnityEditor -or !(Test-Path -LiteralPath $UnityEditor)) { throw 'Pass -UnityEditor with the path to Unity 2020.3.17f1/Editor/Unity.exe.' }
        Write-Output 'Building Unity dependencies and debug material; the first import can take several minutes.'
        $unityArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $UnityProjectDir + '"'),
            '-executeMethod', 'MilestoneBuild.Build', '-logFile', ('"' + "$artifacts/unity-build.log" + '"'))
        $process = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "Unity build failed ($($process.ExitCode)). See artifacts/unity-build.log." }
    }
    $player = "$UnityProjectDir/Build/ShipbreakerVrUnity_Data"
    foreach ($file in @('Managed/Unity.InputSystem.dll','Managed/Unity.XR.Management.dll','Managed/Unity.XR.OpenXR.dll',
        'Plugins/x86_64/UnityOpenXR.dll','Plugins/x86_64/openxr_loader.dll','UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json')) {
        if (!(Test-Path -LiteralPath "$player/$file")) { throw "Missing Unity build output: $file" }
    }
    $restoreArgs = @('restore', 'ShipbreakerVr.sln', '--locked-mode', '--configfile', "$repoRoot/NuGet.Config", '-m:1', '-nodeReuse:false')
    if ($NuGetSource) { $restoreArgs += @('--source', $NuGetSource) }
    & $DotNet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
    & $DotNet build ShipbreakerVr.sln --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false -c $Configuration "-p:GameDir=$GameDir" "-p:UnityProjectDir=$UnityProjectDir"
    if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
    $stage = "$artifacts/$Configuration/Mod"
    $nativeStage = "$stage/BepInEx/patchers/ShipbreakerVrPatcher/CopyToGame/Shipbreaker_Data"
    foreach ($file in @('Plugins/x86_64/UnityOpenXR.dll','Plugins/x86_64/openxr_loader.dll','UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json')) {
        $destination = Join-Path $nativeStage $file
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath "$player/$file" -Destination $destination -Force
    }
    $inputs = @()
    $inputs += Get-ChildItem -LiteralPath "$GameDir/Shipbreaker_Data/Managed" -Filter '*.dll'
    $inputs += Get-ChildItem -LiteralPath "$player/Managed" -Filter 'Unity*.dll'
    $inputs += Get-Item -LiteralPath "$repoRoot/ShipbreakerVrUnity/AssetBundles/xrmanager", "$UnityProjectDir/AssetBundles/debugrays"
    $inputs | Sort-Object FullName | ForEach-Object {
        [PSCustomObject]@{ File=$_.Name; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    } | ConvertTo-Json | Set-Content -LiteralPath "$stage/build-inputs.json" -Encoding UTF8
    Write-Output "Build complete: $stage"
    Write-Output 'No game files were modified. See docs/BUILD.md for install and rollback.'
} finally { Pop-Location }
