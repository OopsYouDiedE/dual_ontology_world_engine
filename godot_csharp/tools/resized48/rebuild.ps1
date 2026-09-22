param([Parameter(Mandatory=$true)][string]$Godot)
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$logRoot = Join-Path $projectRoot '../artifacts/resized48-shots'
New-Item -ItemType Directory -Force $logRoot | Out-Null
foreach ($script in @('layout_shot01','layout_shot02')) {
    $log = Join-Path $logRoot ($script + '.log')
    $arguments = @('--path', $projectRoot, '--rendering-method', 'gl_compatibility', '--log-file', $log, '--script', "res://tools/resized48/$script.gd", '--quit-after', '240')
    $process = Start-Process -FilePath $Godot -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or (Select-String -Path $log -Pattern 'SCRIPT ERROR|Assertion failed|Cannot save file' -Quiet)) { throw "Build failed: $log" }
}
