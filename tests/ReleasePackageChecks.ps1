#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Zip, [string]$LegacyGameDir)
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot ('../artifacts/package-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$root = (Resolve-Path -LiteralPath $root).Path
Expand-Archive -LiteralPath $Zip -DestinationPath "$root/extracted"
$package = @(Get-ChildItem -LiteralPath "$root/extracted" -Directory)
if ($package.Count -ne 1) { throw 'Expected one package directory.' }
$package = $package[0].FullName
$manifest = Get-Content -LiteralPath "$package/manifest.json" -Raw | ConvertFrom-Json
$checks = 0
function Assert([bool]$Result,[string]$Message) {
    if (!$Result) { throw "FAILED: $Message" }; $script:checks++; Write-Output "PASS: $Message"
}
foreach ($line in Get-Content -LiteralPath "$package/SHA256SUMS.txt") {
    if ($line -notmatch '^([A-F0-9]{64})  (.+)$') { throw 'Invalid checksum line.' }
    $hash = $Matches[1]; $file = Join-Path $package $Matches[2]
    if ((Get-FileHash -LiteralPath $file).Hash -ne $hash) { throw "Checksum mismatch: $file" }
}
Assert $true 'all extracted package checksums match'
$files = @(Get-ChildItem -LiteralPath $package -File -Recurse)
Assert (@($files | Where-Object { $_.Name -match '\.(pdb|log)$|^(BBI\.|Assembly-CSharp|UnityEngine\.|UnityEditor\.|Shipbreaker\.exe|ShipbreakerVr\.cfg)' }).Count -eq 0) 'no game assemblies, symbols, logs, executable or personal mod config'
$text = @($files | Where-Object { $_.Extension -in @('.md','.ps1','.cmd','.json','.txt','.cfg','.ini') } | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$workspaceLabel = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Leaf
foreach ($identifier in @($env:USERNAME, $env:COMPUTERNAME, $workspaceLabel)) {
    if ($identifier -and $identifier.Length -ge 3) {
        Assert ($text -notmatch [regex]::Escape($identifier)) "portable text excludes local identifier: $identifier"
    }
}
Assert ($text -notmatch 'installation-backup/update|work/dotnet-home') 'portable text contains no legacy backup or development SDK paths'
function Put([string]$Path,[string]$Text) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    [IO.File]::WriteAllText($Path,$Text)
}
function Run([string]$Action) {
    $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package/Setup.ps1" -Action $Action -GameDir $game -NonInteractive 2>&1
    Assert ($LASTEXITCODE -eq 0) ("$Action in full-payload fixture; " + ($out -join ' '))
}
foreach ($scenario in @('fresh','legacy')) {
    if ($scenario -eq 'legacy' -and !$LegacyGameDir) { continue }
    $game = Join-Path $root "$scenario game"
    Put "$game/Shipbreaker.exe" 'fixture - never executed'
    Put "$game/Shipbreaker_Data/Managed/BBI.Unity.Game.dll" 'fixture - never loaded'
    Put "$game/unrelated-save.dat" 'preserve'
    $before = @{}
    if ($scenario -eq 'legacy') {
        foreach ($entry in $manifest.Files) {
            $source = Join-Path $LegacyGameDir $entry.Path
            if (Test-Path -LiteralPath $source -PathType Leaf) {
                $target = Join-Path $game $entry.Path
                New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
                Copy-Item -LiteralPath $source -Destination $target
                $before[$entry.Path] = (Get-FileHash -LiteralPath $source).Hash
            }
        }
        Copy-Item -LiteralPath "$LegacyGameDir/BepInEx/config/ShipbreakerVr.cfg" -Destination "$game/BepInEx/config/ShipbreakerVr.cfg"
    }
    Run Check
    Run Install
    foreach ($entry in $manifest.Files) {
        if ($entry.Path -eq 'BepInEx/config/BepInEx.cfg' -and $before.ContainsKey($entry.Path)) { continue }
        if ((Get-FileHash -LiteralPath (Join-Path $game $entry.Path)).Hash -ne $entry.SHA256) { throw "Installed payload mismatch: $($entry.Path)" }
    }
    Assert $true "$scenario all 35 installed payload hashes verified (shared existing config retained)"
    Run Install
    Run Uninstall
    foreach ($entry in $manifest.Files) {
        $target = Join-Path $game $entry.Path
        if ($entry.Path -eq 'BepInEx/config/BepInEx.cfg') { continue }
        if ($before.ContainsKey($entry.Path)) {
            if ((Get-FileHash -LiteralPath $target).Hash -ne $before[$entry.Path]) { throw "Original not restored: $($entry.Path)" }
        } elseif (Test-Path -LiteralPath $target) { throw "Added file remains: $($entry.Path)" }
    }
    Assert $true "$scenario every mod/native/loader file removed or restored after repeated install"
    Assert ((Get-Content -LiteralPath "$game/unrelated-save.dat" -Raw) -eq 'preserve') "$scenario unrelated data retained"
    if ($scenario -eq 'legacy') {
        $cfg = Get-Content -LiteralPath "$game/BepInEx/config/ShipbreakerVr.cfg" -Raw
        Assert ($cfg -match 'PanelWidthMetres = 1.6728' -and $cfg -match 'VerticalLayoutScale = 1.16' -and $cfg -notmatch 'CaptureHudCacheOnce|CaptureUiOptimizationOnce') 'accepted tuning retained and retired tests removed'
    }
}
Write-Output "$checks full-package checks passed. Fixture: $root"
