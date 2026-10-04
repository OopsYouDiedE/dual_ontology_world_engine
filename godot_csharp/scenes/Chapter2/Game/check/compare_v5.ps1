param(
    [string]$OutputDirectory = 'artifacts/remake-chapter2/refactor-v5/auto',
    [string]$Dotnet = 'C:/Program Files/dotnet/dotnet.exe',
    [string]$CliDll = 'godot_csharp/scenes/Chapter2/Prototype/bin/Debug/net8.0/Chapter2Prototype.dll'
)
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $OutputDirectory 'inputs.txt'
$raw = Join-Path $OutputDirectory 'cli-raw-output.txt'
$expectedFile = Join-Path $OutputDirectory 'cli-visible-output.txt'
$actualFile = Join-Path $OutputDirectory 'godot-visible-output.txt'
if (!(Test-Path -LiteralPath $fixture) -or !(Test-Path -LiteralPath $actualFile)) {
    throw 'Run regression_v5.gd to completion before comparing. Partial/failed GUI runs are invalid.'
}
& $Dotnet $CliDll play $fixture > $raw
$cliExit = $LASTEXITCODE
$cliExit | Set-Content (Join-Path $OutputDirectory 'cli.exit')
if ($cliExit -ne 0) { throw "CLI failed: $cliExit" }
# Program play deliberately echoes scripted input. These input-only lines are
# outside ConsoleUI output with echo:false, as used by Chapter2Bridge.
# Preserve every other character, blank line, order, and repeated paragraph.
$expected = @([IO.File]::ReadAllLines((Resolve-Path -LiteralPath $raw)) | Where-Object { !$_.StartsWith('> ') })
$actual = @([IO.File]::ReadAllLines((Resolve-Path -LiteralPath $actualFile)))
[IO.File]::WriteAllLines((Join-Path (Resolve-Path $OutputDirectory) 'cli-visible-output.txt'), [string[]]$expected, [Text.UTF8Encoding]::new($false))
$mismatches = [Collections.Generic.List[string]]::new()
for ($index=0; $index -lt [Math]::Max($expected.Length,$actual.Length); $index++) {
    if ($index -ge $expected.Length -or $index -ge $actual.Length -or $expected[$index] -cne $actual[$index]) {
        $left = if ($index -lt $expected.Length) { $expected[$index] } else { '<EOF>' }
        $right = if ($index -lt $actual.Length) { $actual[$index] } else { '<EOF>' }
        $mismatches.Add("line $($index+1): CLI=[$left] GUI=[$right]")
    }
}
$report = @("CLI lines=$($expected.Length)","GUI lines=$($actual.Length)","Mismatches=$($mismatches.Count)") + $mismatches
$report | Set-Content (Join-Path $OutputDirectory 'transcript-comparison.txt')
$report | Select-Object -First 20
if ($mismatches.Count -gt 0) { exit 1 }
exit 0
