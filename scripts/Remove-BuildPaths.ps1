# Remove only CodeView debug path strings from unsigned, locally built managed DLLs.
# IL, resources, assembly identities and debug identifiers remain byte-for-byte intact.
param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference = 'Stop'
function U16($b,$o) { [BitConverter]::ToUInt16($b,$o) }
function U32($b,$o) { [BitConverter]::ToUInt32($b,$o) }
$changed = 0
$files = @(Get-ChildItem -LiteralPath "$Package/payload/BepInEx/plugins/ShipbreakerVr" -Filter '*.dll' -File)
$files += Get-Item -LiteralPath "$Package/payload/BepInEx/patchers/ShipbreakerVrPatcher/ShipbreakerVrPatcher.dll"
foreach ($file in $files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $original = [byte[]]$bytes.Clone()
    $pe = U32 $bytes 60
    if ([Text.Encoding]::ASCII.GetString($bytes,$pe,4) -ne "PE`0`0") { throw 'Invalid PE header.' }
    $optional = $pe + 24
    $magic = U16 $bytes $optional
    $directory = $optional + $(if ($magic -eq 0x20b) { 112 } elseif ($magic -eq 0x10b) { 96 } else { throw 'Unknown PE format.' })
    $sections = $optional + (U16 $bytes ($pe + 20))
    $sectionCount = U16 $bytes ($pe + 6)
    function Offset([uint32]$Rva) {
        for ($i=0; $i -lt $sectionCount; $i++) {
            $s = $sections + $i * 40
            $virtualSize = U32 $bytes ($s+8); $va = U32 $bytes ($s+12)
            $rawSize = U32 $bytes ($s+16); $raw = U32 $bytes ($s+20)
            if ($Rva -ge $va -and $Rva -lt ($va + [Math]::Max($virtualSize,$rawSize))) { return [int]($raw + $Rva - $va) }
        }
        throw 'Invalid PE RVA.'
    }
    if ((U32 $bytes ($directory + 4*8 + 4)) -ne 0) { throw "Refusing to edit an Authenticode-signed DLL: $($file.Name)" }
    $cli = Offset (U32 $bytes ($directory + 14*8))
    if ((U32 $bytes ($cli+16)) -band 8) { throw "Refusing to edit a strong-name-signed DLL: $($file.Name)" }
    $debugRva = U32 $bytes ($directory+6*8)
    $debugSize = U32 $bytes ($directory+6*8+4)
    if (!$debugRva) { continue }
    $debug = Offset $debugRva
    $allowed = New-Object 'Collections.Generic.HashSet[int]'
    for ($entry=$debug; $entry -lt $debug+$debugSize; $entry+=28) {
        if ((U32 $bytes ($entry+12)) -ne 2) { continue }
        $size = U32 $bytes ($entry+16); $raw = U32 $bytes ($entry+24)
        if ($size -lt 25 -or $raw+$size -gt $bytes.Length) { throw 'Invalid CodeView bounds.' }
        if ([Text.Encoding]::ASCII.GetString($bytes,$raw,4) -ne 'RSDS') { throw 'Unsupported CodeView format.' }
        $start = [int]($raw+24); $count = [int]($size-24)
        $path = [Text.Encoding]::UTF8.GetString($bytes,$start,$count).TrimEnd([char]0)
        if (!$path.Contains('\') -and !$path.Contains('/')) { continue }
        $replacement = [Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFileName($path))
        if ($replacement.Length -ge $count) { throw 'Invalid replacement debug filename.' }
        [Array]::Clear($bytes,$start,$count)
        [Array]::Copy($replacement,0,$bytes,$start,$replacement.Length)
        for ($i=$start; $i -lt $start+$count; $i++) { $null = $allowed.Add($i) }
    }
    if ($allowed.Count -eq 0) { continue }
    for ($i=0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -ne $original[$i] -and !$allowed.Contains($i)) { throw 'Unexpected change outside a debug path.' }
    }
    [IO.File]::WriteAllBytes($file.FullName,$bytes)
    $null = [Reflection.AssemblyName]::GetAssemblyName($file.FullName)
    $changed++
}
Write-Output "Removed debug directory paths from $changed unsigned managed DLLs; executable bytes unchanged."
