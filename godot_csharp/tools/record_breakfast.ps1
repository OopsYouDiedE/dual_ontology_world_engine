param(
    [string]$Godot = 'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe',
    [switch]$Montage
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repository = (Resolve-Path (Join-Path $project '..')).Path
$overridePath = Join-Path $project 'override.cfg'
if (Test-Path -LiteralPath $overridePath) {
    throw 'Existing override.cfg must be preserved; close the other recording first.'
}

$outputDirectory = Join-Path $repository 'artifacts/first-three-scenes'
$temporaryDirectory = Join-Path $repository 'tmp'
$sceneNames = if ($Montage) { @('Breakfast', 'SmallLesson', 'ParlorGathering') } else { @('Breakfast') }
$result = if ($Montage) {
    Join-Path $outputDirectory 'daily-montage.mp4'
} else {
    Join-Path $outputDirectory 'breakfast-action.mp4'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null

try {
    @'
[display]
window/size/viewport_width=1280
window/size/viewport_height=992
window/size/window_width_override=1280
window/size/window_height_override=992
'@ | Set-Content -LiteralPath $overridePath -Encoding utf8

    $clips = @()
    foreach ($sceneName in $sceneNames) {
        $raw = Join-Path $temporaryDirectory "$($sceneName.ToLowerInvariant()).avi"
        $log = Join-Path $outputDirectory "$($sceneName.ToLowerInvariant())-record.log"

        # Remove only this named output so a failed run cannot reuse an old AVI or log.
        Remove-Item -LiteralPath $raw, $log -Force -ErrorAction SilentlyContinue
        & $Godot --path $project --rendering-method gl_compatibility --log-file $log --write-movie $raw --fixed-fps 30 --script res://scenes/Story/record_daily_clip.gd -- $sceneName
        $godotExitCode = $LASTEXITCODE
        if ($godotExitCode -ne 0) { throw "Godot recording failed for $sceneName (exit $godotExitCode)." }
        if (-not (Test-Path -LiteralPath $log) -or (Get-Item -LiteralPath $log).Length -eq 0) {
            throw "Godot did not produce a usable recording log for $sceneName."
        }
        $logText = Get-Content -LiteralPath $log -Raw
        $unexpectedErrors = ($logText -split "`r?`n") | Where-Object {
            $_ -match '^\s*(ERROR|SCRIPT ERROR):' -and $_ -notmatch '^\s*ERROR: Failed to read the root certificate store\.$'
        }
        if ($unexpectedErrors) {
            throw "Godot reported an error while recording $sceneName; see $log."
        }
        if (-not (Test-Path -LiteralPath $raw) -or (Get-Item -LiteralPath $raw).Length -eq 0) {
            throw "Godot did not produce a new AVI for $sceneName."
        }
        $clips += $raw
    }

    if ($Montage) {
        $filterParts = @()
        for ($index = 0; $index -lt $clips.Count; $index++) {
            $filterParts += "[$($index):v]crop=1280:768:0:0,fps=30,setpts=PTS-STARTPTS[v$index]"
            $filterParts += "[$($index):a]aresample=async=1:first_pts=0,asetpts=PTS-STARTPTS[a$index]"
        }
        $concatInputs = -join (0..($clips.Count - 1) | ForEach-Object { "[v$_][a$_]" })
        $filterParts += "$concatInputs concat=n=$($clips.Count):v=1:a=1[outv][outa]"
        $filter = $filterParts -join ';'
        $ffmpegArgs = @('-hide_banner', '-loglevel', 'error')
        foreach ($clip in $clips) { $ffmpegArgs += @('-i', $clip) }
        $ffmpegArgs += @('-filter_complex', $filter, '-map', '[outv]', '-map', '[outa]', '-c:v', 'libx264', '-crf', '17', '-pix_fmt', 'yuv420p', '-r', '30', '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', '-y', $result)
        & ffmpeg @ffmpegArgs
    } else {
        & ffmpeg -hide_banner -loglevel error -i $clips[0] -vf 'crop=1280:768:0:0,fps=30' -c:v libx264 -crf 17 -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart -y $result
    }
    $ffmpegExitCode = $LASTEXITCODE
    if ($ffmpegExitCode -ne 0) { throw "Video encoding failed (exit $ffmpegExitCode)." }
    if (-not (Test-Path -LiteralPath $result) -or (Get-Item -LiteralPath $result).Length -eq 0) {
        throw 'Video encoding completed without producing the expected output.'
    }
} finally {
    Remove-Item -LiteralPath $overridePath -ErrorAction SilentlyContinue
}

Write-Output $result
