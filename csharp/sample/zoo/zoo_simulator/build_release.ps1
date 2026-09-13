param(
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "..\..\publish\cli-win-x64"
)

Write-Host "Packaging ZooSimulator standalone CLI for $Runtime..."
dotnet publish ZooSimulator.csproj -c Release -r $Runtime --self-contained -p:PublishSingleFile=true -o $OutputDir
if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded. Output: $OutputDir\ZooSimulator.exe" -ForegroundColor Green
} else {
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
