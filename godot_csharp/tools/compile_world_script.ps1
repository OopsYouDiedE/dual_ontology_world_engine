# 正式剧本：-Source ../scenes/Awakening/Script/Chapter1Script.cs（第二章同理）。
# 正式入口连同同名工程中的完整原源码、资源一起编译；也可以直接传 .csproj。
# 外置扩展示例仍编译为 WorldScript.<名称>.dll，交给 --world-script 或 run_script 加载。
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Output,
    [string]$ContractProject,
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$godotRoot = Split-Path -Parent $PSScriptRoot
$scriptName = [IO.Path]::GetFileNameWithoutExtension($sourcePath)
$buildDirectory = Join-Path $godotRoot "tools/bin/world-scripts/$scriptName"
if (!$Output) { $Output = Join-Path $buildDirectory 'output' }
$outputPath = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$chapterProject = [IO.Path]::ChangeExtension($sourcePath, '.csproj')
if ([IO.Path]::GetExtension($sourcePath) -eq '.csproj' -or (Test-Path -LiteralPath $chapterProject)) {
    & $Dotnet build $chapterProject --configuration Debug --output $outputPath --nologo
    if ($LASTEXITCODE -ne 0) { throw "章节剧本编译失败（退出码 $LASTEXITCODE）。" }
    Write-Output (Join-Path $outputPath "$scriptName.dll")
    return
}
if (!$ContractProject) { $ContractProject = Join-Path $godotRoot 'scenes/Chapter2/Script/Chapter2Script.csproj' }
$contractPath = (Resolve-Path -LiteralPath $ContractProject).Path
$sourceXml = [System.Security.SecurityElement]::Escape($sourcePath)
$projectXml = [System.Security.SecurityElement]::Escape($contractPath)
$chapter1Xml = [System.Security.SecurityElement]::Escape((Join-Path $godotRoot 'scenes/Awakening/Script/Chapter1Script.csproj'))
$assemblyXml = [System.Security.SecurityElement]::Escape("WorldScript.$scriptName")
$projectPath = Join-Path $buildDirectory 'WorldScript.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>$assemblyXml</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$sourceXml" />
    <ProjectReference Include="$projectXml" />
    <ProjectReference Include="$chapter1Xml" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath -Encoding utf8
& $Dotnet build $projectPath --configuration Debug --output $outputPath --nologo
if ($LASTEXITCODE -ne 0) { throw "世界脚本编译失败（退出码 $LASTEXITCODE）。" }
Write-Output (Join-Path $outputPath "WorldScript.$scriptName.dll")
