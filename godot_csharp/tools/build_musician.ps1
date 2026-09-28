Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$root = Join-Path $projectRoot 'ShareAsset/Internal/Characters/story_musician'
$sources = Join-Path $projectRoot 'AssetsSource/Internal/Characters/StoryMusician'
$mainAtlasPath = Join-Path $root 'musician.png'
$specialAtlasPath = Join-Path $root 'musician_special.png'
$previewPath = Join-Path $root 'musician-preview.png'
$specialPreviewPath = Join-Path $root 'musician-special-preview.png'

function Get-CellSprite([System.Drawing.Bitmap]$sheet, [int]$cols, [int]$rows, [int]$col, [int]$row, [double]$referenceHeight = 0) {
    $left = [int][Math]::Floor($col * $sheet.Width / $cols)
    $right = [int][Math]::Floor(($col + 1) * $sheet.Width / $cols)
    $top = [int][Math]::Floor($row * $sheet.Height / $rows)
    $bottom = [int][Math]::Floor(($row + 1) * $sheet.Height / $rows)
    # The generated walk sheet has deliberately generous, uneven vertical gutters.
    if ($cols -eq 4 -and $rows -eq 3) {
        $top = @(100, 470, 825)[$row]
        $bottom = @(470, 830, 1180)[$row]
    }
    if ($cols -eq 6 -and $rows -eq 3) {
        $top = @(0, 380, 735)[$row]
        $bottom = @(380, 735, 1024)[$row]
    }
    $minX = $right; $minY = $bottom; $maxX = $left - 1; $maxY = $top - 1
    for ($y = $top; $y -lt $bottom; $y++) {
        for ($x = $left; $x -lt $right; $x++) {
            if ($sheet.GetPixel($x, $y).A -gt 192) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt $minX) { throw "Empty source cell $col,$row" }
    $w = $maxX - $minX + 1; $h = $maxY - $minY + 1
    $cell = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($cell)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    if ($referenceHeight -le 0) { $referenceHeight = $h }
    $scale = 43.0 / $referenceHeight
    $dw = [int][Math]::Max(1, [Math]::Round($w * $scale))
    $dh = [int][Math]::Max(1, [Math]::Round($h * $scale))
    $dx = [int][Math]::Floor((64 - $dw) / 2.0)
    $dy = 55 - $dh
    $g.DrawImage($sheet, [System.Drawing.Rectangle]::new($dx, $dy, $dw, $dh), [System.Drawing.Rectangle]::new($minX, $minY, $w, $h), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    for ($py = 0; $py -lt 64; $py++) {
        for ($px = 0; $px -lt 64; $px++) {
            $pixel = $cell.GetPixel($px, $py)
            if ($pixel.A -lt 192) { $cell.SetPixel($px, $py, [System.Drawing.Color]::Transparent) }
            elseif ($pixel.A -lt 255) { $cell.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.G, $pixel.B)) }
        }
    }
    return $cell
}

function Get-CellHeight([System.Drawing.Bitmap]$sheet, [int]$cols, [int]$rows, [int]$col, [int]$row) {
    $left = [int][Math]::Floor($col * $sheet.Width / $cols)
    $right = [int][Math]::Floor(($col + 1) * $sheet.Width / $cols)
    $top = [int][Math]::Floor($row * $sheet.Height / $rows)
    $bottom = [int][Math]::Floor(($row + 1) * $sheet.Height / $rows)
    if ($cols -eq 4 -and $rows -eq 3) { $top = @(100, 470, 825)[$row]; $bottom = @(470, 830, 1180)[$row] }
    if ($cols -eq 6 -and $rows -eq 3) { $top = @(0, 380, 735)[$row]; $bottom = @(380, 735, 1024)[$row] }
    $minY = $bottom; $maxY = $top - 1
    for ($y = $top; $y -lt $bottom; $y++) {
        for ($x = $left; $x -lt $right; $x++) {
            if ($sheet.GetPixel($x, $y).A -gt 192) { if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } }
        }
    }
    if ($maxY -lt $minY) { throw "Empty source cell $col,$row" }
    return $maxY - $minY + 1
}

function Put-Frame([System.Drawing.Bitmap]$atlas, [System.Drawing.Bitmap]$frame, [int]$col, [int]$row) {
    $g = [System.Drawing.Graphics]::FromImage($atlas)
    $g.DrawImageUnscaled($frame, $col * 64, $row * 64)
    $g.Dispose(); $frame.Dispose()
}

$idleTalk = [System.Drawing.Bitmap]::FromFile((Join-Path $sources 'idle-talk-source.png'))
$talk = [System.Drawing.Bitmap]::FromFile((Join-Path $sources 'talk-source.png'))
$walk = [System.Drawing.Bitmap]::FromFile((Join-Path $sources 'walk-source.png'))
$special = [System.Drawing.Bitmap]::FromFile((Join-Path $sources 'listen-source.png'))
$main = New-Object System.Drawing.Bitmap 256, 512, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$extra = New-Object System.Drawing.Bitmap 256, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# Main atlas row order matches the declared SpriteFrames animation rows.
$idleFrontHeight = Get-CellHeight $idleTalk 6 3 0 0
$idleBackHeight = Get-CellHeight $idleTalk 6 3 1 0
$idleLeftHeight = Get-CellHeight $idleTalk 6 3 2 0
$walkFrontHeight = Get-CellHeight $walk 4 3 0 0
$walkBackHeight = Get-CellHeight $walk 4 3 0 1
$walkLeftHeight = Get-CellHeight $walk 4 3 0 2
$talkFrontHeight = Get-CellHeight $talk 3 2 0 0
$talkLeftHeight = Get-CellHeight $talk 3 2 0 1
$listenHeight = Get-CellHeight $special 6 4 1 3
Put-Frame $main (Get-CellSprite $idleTalk 6 3 0 0 $idleFrontHeight) 0 0 # idle_front
Put-Frame $main (Get-CellSprite $idleTalk 6 3 1 0 $idleBackHeight) 0 1 # idle_back
Put-Frame $main (Get-CellSprite $idleTalk 6 3 2 0 $idleLeftHeight) 0 2 # idle_left
for ($i = 0; $i -lt 4; $i++) { Put-Frame $main (Get-CellSprite $walk 4 3 $i 0 $walkFrontHeight) $i 3 }
for ($i = 0; $i -lt 4; $i++) { Put-Frame $main (Get-CellSprite $walk 4 3 $i 1 $walkBackHeight) $i 4 }
for ($i = 0; $i -lt 4; $i++) { Put-Frame $main (Get-CellSprite $walk 4 3 $i 2 $walkLeftHeight) $i 5 }
Put-Frame $main (Get-CellSprite $talk 3 2 0 0 $talkFrontHeight) 0 6
Put-Frame $main (Get-CellSprite $talk 3 2 1 0 $talkFrontHeight) 1 6
Put-Frame $main (Get-CellSprite $talk 3 2 2 0 $talkFrontHeight) 2 6
Put-Frame $main (Get-CellSprite $talk 3 2 0 1 $talkLeftHeight) 0 7
Put-Frame $main (Get-CellSprite $talk 3 2 1 1 $talkLeftHeight) 1 7
Put-Frame $main (Get-CellSprite $talk 3 2 2 1 $talkLeftHeight) 2 7

for ($i = 0; $i -lt 4; $i++) { Put-Frame $extra (Get-CellSprite $special 6 4 ($i + 1) 3 $listenHeight) $i 0 }
$main.Save($mainAtlasPath, [System.Drawing.Imaging.ImageFormat]::Png)
$extra.Save($specialAtlasPath, [System.Drawing.Imaging.ImageFormat]::Png)
$preview = New-Object System.Drawing.Bitmap 1024, 2048, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$previewGraphics = [System.Drawing.Graphics]::FromImage($preview)
$previewGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$previewGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$previewGraphics.DrawImage($main, [System.Drawing.Rectangle]::new(0, 0, 1024, 2048), [System.Drawing.Rectangle]::new(0, 0, 256, 512), [System.Drawing.GraphicsUnit]::Pixel)
$previewGraphics.Dispose()
$preview.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
$specialPreview = New-Object System.Drawing.Bitmap 1024, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$specialPreviewGraphics = [System.Drawing.Graphics]::FromImage($specialPreview)
$specialPreviewGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$specialPreviewGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$specialPreviewGraphics.DrawImage($extra, [System.Drawing.Rectangle]::new(0, 0, 1024, 256), [System.Drawing.Rectangle]::new(0, 0, 256, 64), [System.Drawing.GraphicsUnit]::Pixel)
$specialPreviewGraphics.Dispose()
$specialPreview.Save($specialPreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$specialPreview.Dispose()
$main.Dispose(); $extra.Dispose(); $idleTalk.Dispose(); $talk.Dispose(); $walk.Dispose(); $special.Dispose()

function Write-SpriteFrames($path, $texturePath, $rows, $animations) {
    $sub = [System.Collections.Generic.List[string]]::new()
    $animData = [System.Collections.Generic.List[string]]::new()
    $id = 0
    foreach ($a in $animations) {
        $frames = [System.Collections.Generic.List[string]]::new()
        for ($i = 0; $i -lt $a.Count; $i++) {
            $name = "f_$id"; $id++
            $sub.Add("[sub_resource type=`"AtlasTexture`" id=`"$name`"]`natlas = ExtResource(`"1`")`nregion = Rect2($($i * 64), $($a.Row * 64), 64, 64)")
            $frames.Add('{"duration": 1.0, "texture": SubResource("' + $name + '")}')
        }
        $frameText = $frames -join ', '
        $loop = if ($a.Loop) { 'true' } else { 'false' }
        $animData.Add("{`"frames`": [$frameText], `"loop`": $loop, `"name`": &`"$($a.Name)`", `"speed`": $($a.Speed)}")
    }
    $loadSteps = 2 + $sub.Count
    $body = "[gd_resource type=`"SpriteFrames`" load_steps=$loadSteps format=3]`n`n[ext_resource type=`"Texture2D`" path=`"res://ShareAsset/Internal/Characters/story_musician/$texturePath`" id=`"1`"]`n`n" + ($sub -join "`n`n") + "`n`n[resource]`nanimations = [" + ($animData -join ",`n") + "]`n"
    Set-Content -Path $path -Value $body -Encoding utf8
}

Write-SpriteFrames (Join-Path $root 'musician.tres') 'musician.png' 8 @(
    @{Name='idle_front';Row=0;Count=1;Loop=$true;Speed=7},
    @{Name='idle_back';Row=1;Count=1;Loop=$true;Speed=7},
    @{Name='idle_left';Row=2;Count=1;Loop=$true;Speed=7},
    @{Name='walk_front';Row=3;Count=4;Loop=$true;Speed=10},
    @{Name='walk_back';Row=4;Count=4;Loop=$true;Speed=10},
    @{Name='walk_left';Row=5;Count=4;Loop=$true;Speed=10},
    @{Name='talk_front';Row=6;Count=3;Loop=$false;Speed=7},
    @{Name='talk_left';Row=7;Count=3;Loop=$false;Speed=7}
)
Write-SpriteFrames (Join-Path $root 'special.tres') 'musician_special.png' 1 @(
    @{Name='listen_left';Row=0;Count=4;Loop=$false;Speed=7}
)
Write-Output "Built $mainAtlasPath and $specialAtlasPath"
