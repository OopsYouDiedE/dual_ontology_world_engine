param(
    [Parameter(Mandatory = $true)][string]$OutDirectory,
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$output = [IO.Path]::GetFullPath($OutDirectory)
if (Test-Path -LiteralPath $output) { throw 'Output directory already exists; choose a new evidence directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$entry = Join-Path $PSScriptRoot 'scheduler-boundary-probe.dv'
$generator = Join-Path $repository 'language_core/tools/build_session.py'
$runtime = Join-Path $repository 'language_core/compiler/Runtime.cs'
$generation = Join-Path $output 'generated'
& $Python $generator --entry $entry --main dvg_debug_run --out $generation 1> (Join-Path $output 'generation.stdout') 2> (Join-Path $output 'generation.stderr')
if ($LASTEXITCODE -ne 0) { throw 'Neutral DV generation failed; original diagnostics retained.' }
Copy-Item -LiteralPath $runtime -Destination (Join-Path $generation 'Runtime.cs')
Get-FileHash -Algorithm SHA256 $runtime | Select-Object Path, Hash | ConvertTo-Json | Out-File -FilePath (Join-Path $output 'runtime-source.json') -Encoding utf8
$project = Join-Path $generation 'NeutralSchedulerGuard.csproj'
[IO.File]::WriteAllText($project, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>')
& dotnet build $project -c Release --disable-build-servers -p:NuGetAudit=false 1> (Join-Path $output 'build.stdout') 2> (Join-Path $output 'build.stderr')
if ($LASTEXITCODE -ne 0) { throw 'Neutral executable build failed; original diagnostics retained.' }
$oldRollForward = $env:DOTNET_ROLL_FORWARD
try {
    $env:DOTNET_ROLL_FORWARD = 'Major'
    & dotnet (Join-Path $generation 'bin/Release/net8.0/NeutralSchedulerGuard.dll') 1> (Join-Path $output 'probe.stdout') 2> (Join-Path $output 'probe.stderr')
    if ($LASTEXITCODE -ne 0) { throw 'Neutral helper probe exited abnormally; stdout/stderr retained.' }
} finally { $env:DOTNET_ROLL_FORWARD = $oldRollForward }
& $Python (Join-Path $PSScriptRoot 'read-scheduler-probe.py') $output
if ($LASTEXITCODE -ne 0) { throw 'Neutral helper values differ; original output retained.' }
