$ErrorActionPreference = 'Stop'
function Measure-CoreSource([string] $directory) {
    $files = @(Get-ChildItem -LiteralPath $directory -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })
    $lines = @($files | ForEach-Object { Get-Content -LiteralPath $_.FullName })
    [pscustomobject]@{
        Directory = $directory
        Files = $files.Count
        Lines = $lines.Count
        Nonempty = @($lines | Where-Object { $_.Trim().Length -gt 0 }).Count
    }
}
$baseline = Measure-CoreSource (Join-Path $PSScriptRoot 'legacy/core')
$current = Measure-CoreSource (Join-Path $PSScriptRoot 'core')
[pscustomobject]@{
    Baseline = $baseline
    Current = $current
    ActiveCoreReductionPercent = [Math]::Round(100 * (1 - $current.Lines / $baseline.Lines), 2)
    Scope = '正式 Core 的源码规模比较。legacy 源码仍然保留；不是仓库总代码删除比例，也不是旧业务功能逐项等价的证明。'
} | ConvertTo-Json -Depth 3
