#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Install','Uninstall','Check','Recover')][string]$Action = 'Install',
    [string]$GameDir,
    [switch]$NonInteractive
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

function SafePath([string]$Root, [string]$Relative) {
    if (!$Relative -or $Relative -match '(^[\\/]|:|[<>"|?*]|(^|[\\/])\.\.?([\\/]|$))') { throw "Unsafe relative path: $Relative" }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $path = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (!$path.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes its root: $Relative" }
    # Reject links in every ancestor, including the selected game and package roots.
    $cursor = $path
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked paths are not supported: $cursor" }
        }
        $cursor = Split-Path $cursor -Parent
    }
    return $path
}
function Hash([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return $null }
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Expected a file: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}
function CopyFile([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination -Parent) | Out-Null
    $temporary = $Destination + '.svr-' + [Guid]::NewGuid().ToString('N')
    try {
        Copy-Item -LiteralPath $Source -Destination $temporary
        if ((Hash $temporary) -ne (Hash $Source)) { throw "Copy verification failed: $Destination" }
        Move-Item -LiteralPath $temporary -Destination $Destination -Force
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}
function WriteJson($Value, [string]$Path) {
    $temp = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temp -Encoding UTF8
    Move-Item -LiteralPath $temp -Destination $Path -Force
}
function Kind([string]$Path) {
    $p = $Path.Replace('\','/')
    if ($p -match '^BepInEx/(plugins/ShipbreakerVr|patchers/ShipbreakerVrPatcher)/[^:]+$' -or
        $p -match '^Shipbreaker_Data/Plugins/x86_64/(UnityOpenXR|openxr_loader)\.dll$' -or
        $p -eq 'Shipbreaker_Data/UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json') { return 'Mod' }
    if ($p -match '^BepInEx/core/[^/]+$' -or $p -in @('winhttp.dll','doorstop_config.ini')) { return 'Shared' }
    if ($p -eq 'BepInEx/config/BepInEx.cfg') { return 'Seed' }
    throw "Unexpected payload destination: $p"
}
function FindGames {
    $roots = @()
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam')) {
        if (Test-Path $key) {
            $entry = Get-ItemProperty $key
            foreach ($property in @('SteamPath','InstallPath')) {
                if ($entry.PSObject.Properties[$property]) { $roots += $entry.$property }
            }
        }
    }
    if (${env:ProgramFiles(x86)}) { $roots += Join-Path ${env:ProgramFiles(x86)} 'Steam' }
    $libraries = @($roots)
    foreach ($root in $roots) {
        $vdf = Join-Path $root 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '"path"\s+"([^"\r\n]+)"')) { $libraries += $match.Groups[1].Value.Replace('\\','\') }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $manifest = Join-Path $library 'steamapps/appmanifest_1161580.acf'
        if (Test-Path -LiteralPath $manifest) {
            $match = [regex]::Match([IO.File]::ReadAllText($manifest), '"installdir"\s+"([^"\r\n]+)"')
            if ($match.Success) {
                $candidate = SafePath (Join-Path $library 'steamapps/common') $match.Groups[1].Value
                if (Test-Path -LiteralPath (Join-Path $candidate 'Shipbreaker.exe')) { $candidate }
            }
        }
    }
}
function SelectGame {
    $games = @(FindGames | Select-Object -Unique)
    if ($games.Count -eq 1) { return $games[0] }
    if ($NonInteractive) { throw 'Specify -GameDir: Steam did not identify exactly one Shipbreaker installation.' }
    Write-Host 'Select the game folder containing Shipbreaker.exe.'
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Select Hardspace Shipbreaker (the folder containing Shipbreaker.exe)'
    $dialog.ShowNewFolderButton = $false
    try { if ($dialog.ShowDialog() -eq 'OK') { return $dialog.SelectedPath } } finally { $dialog.Dispose() }
    throw 'No game folder selected. Nothing changed.'
}
function CleanConfig([string]$Text) {
    $obsolete = @('Controllers/DebugRays','Avatar/AlignDetonatorTop','Avatar/VisualAdjustments','Avatar/BodyVisualScale',
        'HUD/VisorContour','HUD/CurveRadiusMetres','HUD/WidthMetres','Menus/PreviousTab','Menus/NextTab','Menus/MiscAction1','Menus/MiscAction2',
        'Performance/CaptureUiOptimizationOnce','Performance/CaptureHudCacheOnce')
    $section = ''; $pending = New-Object 'Collections.Generic.List[string]'; $result = New-Object 'Collections.Generic.List[string]'
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line -match '^\s*[#;]') { $pending.Add($line); continue }
        if ($line -match '^\s*\[([^\]]+)\]') { $section = $Matches[1] }
        $skip = $line -match '^\s*([^=#;]+?)\s*=' -and ($obsolete -contains ($section + '/' + $Matches[1].Trim()))
        if (!$skip) { $result.AddRange($pending); $result.Add($line) }
        $pending.Clear()
    }
    $result.AddRange($pending)
    return [string]::Join("`r`n", $result)
}
function Recovery([string]$Root, [string]$StateRoot) {
    $journalPath = SafePath $StateRoot 'pending.json'
    if (!(Test-Path -LiteralPath $journalPath)) { return }
    $journal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
    if ($journal.Schema -ne 1) { throw 'Unknown recovery journal. Keep the backup directory intact.' }
    foreach ($op in $journal.Operations) {
        if ($op.Path -notin @('BepInEx/config/ShipbreakerVr.cfg','ShipbreakerVR-InstallState/state.json')) { $null = Kind $op.Path }
        $target = SafePath $Root $op.Path
        $current = Hash $target
        if ($current -ne $op.Before -and $current -ne $op.After) { throw "Recovery blocked by a changed file: $($op.Path). Keep the backups and request assistance." }
        if ($op.Before) {
            $backup = SafePath $StateRoot $op.Backup
            if ((Hash $backup) -ne $op.Before) { throw "Recovery backup failed verification: $($op.Path)" }
        }
    }
    $operations = @($journal.Operations)
    [array]::Reverse($operations)
    foreach ($op in $operations) {
        $target = SafePath $Root $op.Path
        if ($op.Before) { CopyFile (SafePath $StateRoot $op.Backup) $target }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    }
    Remove-Item -LiteralPath $journalPath -Force
    Write-Host 'Recovered the previous installation after an interrupted operation.'
}

try {
    if (!$GameDir) { $GameDir = SelectGame }
    $GameDir = (Resolve-Path -LiteralPath $GameDir).Path
    foreach ($required in @('Shipbreaker.exe','Shipbreaker_Data/Managed/BBI.Unity.Game.dll')) {
        if (!(Test-Path -LiteralPath (SafePath $GameDir $required) -PathType Leaf)) { throw "Not a supported game folder: missing $required" }
    }
    if (Get-Process -Name 'Shipbreaker' -ErrorAction SilentlyContinue) { throw 'Close Shipbreaker before running setup.' }
    $stateRoot = SafePath $GameDir 'ShipbreakerVR-InstallState'
    $statePath = SafePath $stateRoot 'state.json'
    $journalPath = SafePath $stateRoot 'pending.json'
    if (Test-Path -LiteralPath $journalPath) {
        if ($Action -ne 'Recover') { throw 'An interrupted operation needs recovery. Run Recover.cmd with the game closed.' }
        Recovery $GameDir $stateRoot
    }
    if ($Action -eq 'Recover') { Write-Host 'Recovery complete. Run Install.cmd or Uninstall.cmd next.'; exit 0 }
    $state = $null; $old = @{}
    if (Test-Path -LiteralPath $statePath) {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($state.Schema -ne 1) { throw 'Unsupported installer state. Keep the backups intact.' }
        if ($state.Active) {
            foreach ($file in $state.Files) {
                $null = Kind $file.Path
                if ($old.ContainsKey($file.Path)) { throw 'Duplicate installed-file record.' }
                if ((Hash (SafePath $GameDir $file.Path)) -ne $file.InstalledHash) { throw "Installed file was changed or removed: $($file.Path). Setup stopped to preserve external changes." }
                if ($file.OriginalHash -and (Hash (SafePath $stateRoot $file.Backup)) -ne $file.OriginalHash) { throw "Original backup failed verification: $($file.Path)" }
                $old[$file.Path] = $file
            }
        }
    }
    if ($Action -eq 'Uninstall' -and (!$state -or !$state.Active)) { throw 'No active portable installation found. Legacy/manual installations are not removed by this uninstaller.' }
    $manifest = $null; $incoming = @{}
    if ($Action -in @('Install','Check')) {
        $manifest = Get-Content -LiteralPath (SafePath $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
        if ($manifest.Schema -ne 1 -or !$manifest.Version -or @($manifest.Files).Count -lt 1) { throw 'Invalid package manifest.' }
        foreach ($file in $manifest.Files) {
            $null = Kind $file.Path
            if ($incoming.ContainsKey($file.Path)) { throw 'Duplicate package destination.' }
            $source = SafePath (SafePath $PSScriptRoot 'payload') $file.Path
            if ((Hash $source) -ne $file.SHA256) { throw "Package file failed verification: $($file.Path). Extract a fresh copy of the ZIP." }
            $target = SafePath $GameDir $file.Path
            if ((Kind $file.Path) -eq 'Shared' -and !$old.ContainsKey($file.Path)) {
                $existing = Hash $target
                if ($existing -and $existing -ne $file.SHA256) { throw "A different shared mod loader already exists: $($file.Path). Setup will not replace another loader. Use a clean game folder or resolve that conflict first." }
            }
            $incoming[$file.Path] = $file
        }
        if (!$incoming.ContainsKey('BepInEx/plugins/ShipbreakerVr/ShipbreakerVr.dll')) { throw 'The package has no mod DLL.' }
    }
    if ($Action -eq 'Check') { Write-Host "Package and game-folder preflight passed: $GameDir"; exit 0 }

    # All validation above is read-only. No game writes occur until the plan is complete.
    $generation = [Guid]::NewGuid().ToString('N')
    $transactionRelative = 'transactions/' + $generation
    $transactionRoot = SafePath $stateRoot $transactionRelative
    New-Item -ItemType Directory -Path $transactionRoot -Force | Out-Null
    $ops = New-Object 'Collections.Generic.List[object]'
    $records = New-Object 'Collections.Generic.List[object]'
    function Plan([string]$Relative, [string]$Source) {
        $target = SafePath $GameDir $Relative
        $before = Hash $target
        $after = if ($Source) { Hash $Source } else { $null }
        if ($before -eq $after) { return }
        $backupRelative = $transactionRelative + '/before/' + $Relative
        if ($before) { CopyFile $target (SafePath $stateRoot $backupRelative) }
        $ops.Add([pscustomobject]@{ Path=$Relative; Before=$before; After=$after; Backup=$backupRelative; Source=$Source })
    }
    $keepShared = $false
    if ($Action -eq 'Uninstall') {
        foreach ($folder in @('plugins','patchers')) {
            $folderPath = SafePath $GameDir ('BepInEx/' + $folder)
            if (Test-Path -LiteralPath $folderPath) {
                # Never traverse links while deciding whether another mod needs the loader.
                foreach ($entry in Get-ChildItem -LiteralPath $folderPath -Force) {
                    $own = if ($folder -eq 'plugins') { 'ShipbreakerVr' } else { 'ShipbreakerVrPatcher' }
                    if ($entry.Name -ne $own) { $keepShared = $true }
                }
            }
        }
    }
    foreach ($file in $old.Values) {
        if ($Action -eq 'Install' -and $incoming.ContainsKey($file.Path)) { continue }
        if ($keepShared -and (Kind $file.Path) -eq 'Shared') { continue }
        $source = if ($file.OriginalHash) { SafePath $stateRoot $file.Backup } else { $null }
        Plan $file.Path $source
    }
    if ($Action -eq 'Install') {
        foreach ($file in $manifest.Files) {
            $target = SafePath $GameDir $file.Path
            $source = SafePath (SafePath $PSScriptRoot 'payload') $file.Path
            $kind = Kind $file.Path
            if ($kind -eq 'Seed') {
                if (!(Test-Path -LiteralPath $target)) { Plan $file.Path $source }
                continue # User configuration is never owned or removed on uninstall.
            }
            if ($old.ContainsKey($file.Path)) { $record = $old[$file.Path]; $record.InstalledHash = $file.SHA256 }
            else {
                $original = Hash $target
                $backupRelative = 'originals/' + $generation + '/' + $file.Path
                if ($original) { CopyFile $target (SafePath $stateRoot $backupRelative) }
                $record = [pscustomobject]@{ Path=$file.Path; OriginalHash=$original; InstalledHash=$file.SHA256; Backup=$backupRelative }
            }
            $records.Add($record)
            Plan $file.Path $source
        }
        $configRelative = 'BepInEx/config/ShipbreakerVr.cfg'
        $configPath = SafePath $GameDir $configRelative
        if (Test-Path -LiteralPath $configPath) {
            $text = [IO.File]::ReadAllText($configPath)
            $clean = CleanConfig $text
            if ($clean -ne $text) {
                $cleanPath = SafePath $transactionRoot 'clean-settings.cfg'
                [IO.File]::WriteAllText($cleanPath, $clean, (New-Object Text.UTF8Encoding($false)))
                Plan $configRelative $cleanPath
            }
        }
    }
    $newState = [pscustomobject]@{ Schema=1; Active=($Action -eq 'Install'); Version=$(if ($manifest) { $manifest.Version } else { $state.Version }); Files=@($records.ToArray()) }
    $newStatePath = SafePath $transactionRoot 'new-state.json'
    WriteJson $newState $newStatePath
    Plan 'ShipbreakerVR-InstallState/state.json' $newStatePath
    WriteJson ([pscustomobject]@{Schema=1; Operations=@($ops.ToArray())}) $journalPath
    try {
        foreach ($op in $ops) {
            $target = SafePath $GameDir $op.Path
            if ((Hash $target) -ne $op.Before) { throw "File changed during setup: $($op.Path)" }
            if ($op.Source) { CopyFile $op.Source $target }
            elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
            if ((Hash $target) -ne $op.After) { throw "Write verification failed: $($op.Path)" }
        }
        Remove-Item -LiteralPath $journalPath -Force
    } catch {
        $failure = $_
        try { Recovery $GameDir $stateRoot } catch { Write-Warning "Automatic rollback could not finish: $($_.Exception.Message). Keep ShipbreakerVR-InstallState and run Recover.cmd." }
        throw $failure
    }
    if ($Action -eq 'Install') {
        Write-Host "Shipbreaker VR $($manifest.Version) installed in $GameDir"
        Write-Host 'Connect your headset, start SteamVR with SteamVR as the active OpenXR runtime, then launch the game from Steam.'
    } else {
        Write-Host 'Portable installation removed; previous files restored. Settings, logs, saves and backups were kept.'
        if ($keepShared) { Write-Host 'Shared BepInEx loader retained because other plugin/patcher folders exist.' }
        Write-Host 'If a VR mod existed before this installation, that earlier mod has been restored.'
    }
    exit 0
} catch {
    Write-Host ('Setup stopped: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
