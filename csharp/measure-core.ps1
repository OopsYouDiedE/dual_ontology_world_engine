$ErrorActionPreference = 'Stop'

$sourceRoot = $PSScriptRoot
$sourceRootPrefix = [System.IO.Path]::GetFullPath($sourceRoot).TrimEnd([char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
$generatedPathPattern = '[\\/](bin|obj)[\\/]'
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch $generatedPathPattern } |
    Sort-Object FullName)

$fileMeasurements = foreach ($file in $sourceFiles) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    [pscustomobject]@{
        Path = $file.FullName.Substring($sourceRootPrefix.Length).Replace('\', '/')
        Lines = $lines.Length
        NonemptyLines = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
    }
}

[pscustomobject]@{
    Scope = 'Current C# Core source under csharp/; recursively includes .cs files and excludes generated bin/ and obj/ trees.'
    SourceFiles = $fileMeasurements.Count
    TotalLines = ($fileMeasurements | Measure-Object -Property Lines -Sum).Sum
    NonemptyLines = ($fileMeasurements | Measure-Object -Property NonemptyLines -Sum).Sum
    Files = @($fileMeasurements)
} | ConvertTo-Json -Depth 4
