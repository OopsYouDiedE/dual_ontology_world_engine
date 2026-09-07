param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "zoo_web.csproj"
$output = Join-Path $PSScriptRoot "publish_wasm"
# 旧发布内容已由项目排除，不会再次作为网站输入。
dotnet publish $project -c $Configuration -o $output
if ($LASTEXITCODE -ne 0) { throw "动物园 WebAssembly 发布失败。" }
Set-Content -LiteralPath (Join-Path $output "wwwroot/.nojekyll") -Value "" -Encoding utf8
