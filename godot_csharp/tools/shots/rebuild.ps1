param([Parameter(Mandatory=$true)][string]$Godot)
# Compatibility batch command: rebuilds source-derived prop textures only.
# layout_shot01.gd and layout_shot02.gd do not write Shot01/Shot02 scenes.
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$logRoot = Join-Path $projectRoot '../artifacts/shots'
New-Item -ItemType Directory -Force $logRoot | Out-Null
foreach ($script in @('layout_shot01','layout_shot02')) {
    $log = Join-Path $logRoot ($script + '.log')
    $arguments = @('--path', $projectRoot, '--rendering-method', 'gl_compatibility', '--log-file', $log, '--script', "res://tools/shots/$script.gd", '--quit-after', '240')
    $process = Start-Process -FilePath $Godot -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or (Select-String -Path $log -Pattern 'SCRIPT ERROR|Assertion failed|Cannot save file' -Quiet)) { throw "Build failed: $log" }
}
$storyLog = Join-Path $logRoot 'build_story_tiles.log'
$storyArguments = @('--path', $projectRoot, '--rendering-method', 'gl_compatibility', '--log-file', $storyLog, '--script', 'res://tools/build_story_tiles.gd', '--quit-after', '240')
$storyProcess = Start-Process -FilePath $Godot -ArgumentList $storyArguments -WindowStyle Hidden -PassThru
$storyProcess.WaitForExit()
if ($storyProcess.ExitCode -ne 0 -or (Select-String -Path $storyLog -Pattern 'SCRIPT ERROR|Assertion failed|Cannot save file' -Quiet)) { throw "Story asset build failed: $storyLog" }
