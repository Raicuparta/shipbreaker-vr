# Integration checks run in Windows PowerShell 5.1, matching the public launchers.
[CmdletBinding()]
param([string]$Setup)
$ErrorActionPreference = 'Stop'
if (!$Setup) { $Setup = Join-Path $PSScriptRoot '../scripts/portable/Setup.ps1' }
$root = Join-Path $PSScriptRoot ('../artifacts/installer-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$root = (Resolve-Path -LiteralPath $root).Path
$package = Join-Path $root 'portable package'
$game = Join-Path $root 'Steam Library/game with spaces'
$checks = 0
function Put([string]$Path,[string]$Text) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    [IO.File]::WriteAllText($Path,$Text)
}
function Assert([bool]$Result,[string]$Message) {
    if (!$Result) { throw "FAILED: $Message" }; $script:checks++; Write-Output "PASS: $Message"
}
function Run([string]$Action,[bool]$Success=$true) {
    $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Setup.ps1" -Action $Action -GameDir $game -NonInteractive 2>&1
    Assert (($LASTEXITCODE -eq 0) -eq $Success) ("$Action expected success=$Success; " + ($out -join ' '))
}
function Manifest([string]$Version='test.1') {
    $files = @(Get-ChildItem "$package/payload" -File -Recurse | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring((Join-Path $package 'payload').Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
    [pscustomobject]@{Schema=1;Version=$Version;Files=$files} | ConvertTo-Json -Depth 5 | Set-Content "$package/manifest.json" -Encoding UTF8
}
Put "$package/payload/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" 'new mod 1'
Put "$package/payload/BepInEx/core/BepInEx.dll" 'shared loader'
Put "$package/payload/winhttp.dll" 'doorstop'
Put "$package/payload/Shipbreaker_Data/Plugins/x86_64/UnityOpenXR.dll" 'xr new'
Put "$package/payload/BepInEx/config/BepInEx.cfg" 'fresh config'
Copy-Item -LiteralPath $Setup -Destination "$package/Setup.ps1"
Manifest
Put "$game/Shipbreaker.exe" 'game fixture executable'
Put "$game/Shipbreaker_Data/Managed/BBI.Unity.Game.dll" 'game fixture assembly'
Put "$game/save-do-not-touch.dat" 'save'
Run Check
Assert (!(Test-Path "$game/ShipbreakerVR-InstallState")) 'Check is read-only'
Run Install
Assert ((Get-Content "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" -Raw) -eq 'new mod 1') 'fresh install'
Put "$game/BepInEx/config/BepInEx.cfg" 'user edited loader settings'
Put "$game/BepInEx/config/ShipbreakerVr.cfg" "[HUD]`n# old width`nWidthMetres = 99`nPanelWidthMetres = 1.9`n[Performance]`nCaptureHudCacheOnce = true`nCaptureNextWorkyard = false`n[OtherMod]`nWidthMetres = 55"
Run Install
$cfg = Get-Content "$game/BepInEx/config/ShipbreakerVr.cfg" -Raw
Assert ($cfg -match 'PanelWidthMetres = 1.9' -and $cfg -match 'WidthMetres = 55' -and $cfg -notmatch 'WidthMetres = 99|CaptureHudCacheOnce|# old width') 'remove only obsolete section/key pairs; retain user tuning'
Assert ((Get-Content "$game/BepInEx/config/BepInEx.cfg" -Raw) -eq 'user edited loader settings') 'preserve shared settings'
Put "$package/payload/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" 'new mod 2'
Manifest 'test.2'
Run Install
Assert ((Get-Content "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" -Raw) -eq 'new mod 2') 'update owned files'
Run Uninstall
Assert (!(Test-Path "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll") -and !(Test-Path "$game/Shipbreaker_Data/Plugins/x86_64/UnityOpenXR.dll") -and !(Test-Path "$game/winhttp.dll")) 'fresh uninstall removes mod/native/loader files'
Assert ((Get-Content "$game/save-do-not-touch.dat" -Raw) -eq 'save' -and (Test-Path "$game/BepInEx/config/ShipbreakerVr.cfg")) 'uninstall preserves saves and settings'
Put "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" 'legacy mod'
Put "$game/Shipbreaker_Data/Plugins/x86_64/UnityOpenXR.dll" 'legacy xr'
Run Install
Run Install
Run Uninstall
Assert ((Get-Content "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" -Raw) -eq 'legacy mod' -and (Get-Content "$game/Shipbreaker_Data/Plugins/x86_64/UnityOpenXR.dll" -Raw) -eq 'legacy xr') 'legacy upgrade and repeated install restore original files'
Put "$game/winhttp.dll" 'different loader'
Run Install $false
Assert ((Get-Content "$game/winhttp.dll" -Raw) -eq 'different loader') 'conflicting loader untouched'
Remove-Item -LiteralPath "$game/winhttp.dll"
Put "$package/payload/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" 'tampered package'
Run Install $false
Manifest
Run Install
Put "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" 'external modification'
Run Uninstall $false
Assert ((Get-Content "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" -Raw) -eq 'external modification') 'changed installed file is preserved'
Put "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" 'tampered package'
Put "$game/BepInEx/plugins/OtherMod/mod.dll" 'other mod'
Run Uninstall
Assert ((Test-Path "$game/winhttp.dll") -and (Test-Path "$game/BepInEx/plugins/OtherMod/mod.dll")) 'other mods retain shared loader'
$manifest = Get-Content "$package/manifest.json" -Raw | ConvertFrom-Json
$manifest.Files[0].Path = '../outside.txt'
$manifest | ConvertTo-Json -Depth 5 | Set-Content "$package/manifest.json"
Run Install $false
Manifest
# Exercise a durable interrupted-operation journal with an already replaced file.
$backup = "$game/ShipbreakerVR-InstallState/transactions/recovery/before/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll"
Put $backup 'legacy mod'
Put "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" 'partial install'
$journal = [pscustomobject]@{Schema=1;Operations=@([pscustomobject]@{Path='BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll';Before=(Get-FileHash $backup).Hash;After=(Get-FileHash "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll").Hash;Backup='transactions/recovery/before/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll'})}
$journal | ConvertTo-Json -Depth 5 | Set-Content "$game/ShipbreakerVR-InstallState/pending.json"
Run Install $false
Run Recover
Assert ((Get-Content "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" -Raw) -eq 'legacy mod') 'recover interrupted operation'
Run Recover
$outside = Join-Path $root 'outside game'
New-Item -ItemType Directory -Path $outside | Out-Null
$linked = Join-Path $game 'BepInEx/patchers'
New-Item -ItemType Junction -Path $linked -Target $outside | Out-Null
Put "$package/payload/BepInEx/patchers/ShipbreakerVrPatcher/ShipbreakerVrPatcher.dll" 'patcher'
Manifest
Run Install $false
Assert (@(Get-ChildItem -LiteralPath $outside -Force).Count -eq 0) 'junction target stays untouched'
# Fixture link is removed as a link, never recursively; both paths were checked above.
[IO.Directory]::Delete($linked)
# A file occupying an intended directory causes a mid-transaction copy failure.
Put "$game/BepInEx/patchers" 'directory collision'
$lateFailure = Get-Content "$package/manifest.json" -Raw | ConvertFrom-Json
$lateFailure.Files = @($lateFailure.Files | Sort-Object @{Expression={if ($_.Path -like 'BepInEx/patchers/*') { 1 } else { 0 }}})
$lateFailure | ConvertTo-Json -Depth 5 | Set-Content "$package/manifest.json"
Run Install $false
Assert ((Get-Content "$game/BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll" -Raw) -eq 'legacy mod') 'failed install rolls previously copied files back'
Assert (!(Test-Path "$game/ShipbreakerVR-InstallState/pending.json")) 'successful automatic rollback clears journal'
$game = Join-Path $root 'not a game'
New-Item -ItemType Directory -Path $game | Out-Null
Run Install $false
Write-Output "$checks portable installer integration checks passed. Fixture: $root"
