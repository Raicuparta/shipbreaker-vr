param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
# Use a private test copy. Never edit the installed game's assembly. The desktop
# CLR cannot execute Unity's native clock; replace only that timestamp read with
# a constant for button commit tests. Input tables and update logic stay intact.
$repo = Split-Path $PSScriptRoot -Parent
Add-Type -Path "$repo/ShipbreakerVr/ModFiles/BepInEx/core/Mono.Cecil.dll"
$inputFile = Join-Path $GameDir 'Shipbreaker_Data/Managed/InControl.dll'
$outputFile = Join-Path $PSScriptRoot 'bin/Release/net48/InControl.dll'
if (!(Test-Path -LiteralPath (Split-Path $outputFile -Parent))) { throw 'Build the Release test project first.' }
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($inputFile)
try {
    $commit = $assembly.MainModule.GetType('InControl.OneAxisInputControl').Methods | Where-Object Name -eq 'Commit'
    $calls = @($commit.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -eq 'System.Single UnityEngine.Time::get_realtimeSinceStartup()' })
    if ($calls.Count -ne 1) { throw 'Unexpected InControl clock call; fixture not prepared.' }
    $calls[0].OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_R4
    $calls[0].Operand = [single]1
    $assembly.Write($outputFile)
    Write-Output 'Prepared private InControl fixture: deterministic clock; original input/update logic.'
} finally { $assembly.Dispose() }
