Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
foreach ($dir in @('长旗', '横旗', '纹样', '图集', '预览', '预览/合成帧')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root $dir) | Out-Null
}
function Save-Png($bitmap, $relative) {
    $bitmap.Save((Join-Path $root $relative), [System.Drawing.Imaging.ImageFormat]::Png)
}
function Resize-Pixel($source, [int]$width, [int]$height) {
    $result = [System.Drawing.Bitmap]::new($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($result)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $width, $height), 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    return ,$result
}
$names = @('皇室团龙', '皇家科学院', '神秘学公会', '学院', '伯爵个人')
$allFrames = @{}
$manifest = [ordered]@{ tile_size = 48; columns = 'animation frames 0..7'; rows = $names; animations = @() }
foreach ($kind in @('长旗', '横旗')) {
    $isLong = $kind -eq '长旗'
    $height = if ($isLong) { 96 } else { 48 }
    $fps = if ($isLong) { 6 } else { 10 }
    $scale = if ($isLong) { 0.31 } else { 0.20 }
    $source = [System.Drawing.Bitmap]::new((Join-Path $root "源图/${kind}八帧.png"))
    # Generated sheets have outer margins and nonuniform row spacing. These are
    # measured source bands, not assumed tile coordinates. Scale stays constant.
    $rows = if ($isLong) { @(44, 300, 563, 825, 1084, 1335) } else { @(40, 220, 404, 590, 774, 949) }
    $centers = if ($isLong) { @(74, 214, 354, 494, 634, 774, 914, 1054) } else { @(41, 238, 436, 633, 831, 1027, 1224, 1421) }
    $atlas = [System.Drawing.Bitmap]::new(384, ($height * 5))
    $ag = [System.Drawing.Graphics]::FromImage($atlas)
    for ($faction = 0; $faction -lt 5; $faction++) {
        $directory = Join-Path $root "$kind/$($names[$faction])"
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        $strip = [System.Drawing.Bitmap]::new(384, $height)
        $sg = [System.Drawing.Graphics]::FromImage($strip)
        $frames = @()
        for ($frame = 0; $frame -lt 8; $frame++) {
            $cx = $centers[$frame]
            $left = if ($isLong) { $cx - 69 } else { $cx - 22 }
            $right = if ($isLong) { [Math]::Min($source.Width, $cx + 69) } else { [Math]::Min($source.Width, $cx + 172) }
            $top = $rows[$faction]; $bottom = $rows[$faction + 1]
            # Align by the mounting hardware, never by the moving cloth bounds.
            $capTop = $bottom
            $anchorMinX = $right; $anchorMaxX = $left
            $scanRight = if ($isLong) { $right } else { $cx + 18 }
            for ($y = $top; $y -lt ($top + 40); $y++) {
                for ($x = $left; $x -lt $scanRight; $x++) {
                    if ($source.GetPixel($x, $y).A -ge 192) { $capTop = [Math]::Min($capTop, $y) }
                }
            }
            for ($y = $capTop; $y -lt ($capTop + 18); $y++) {
                for ($x = $left; $x -lt $scanRight; $x++) {
                    if ($source.GetPixel($x, $y).A -ge 192) {
                        $anchorMinX = [Math]::Min($anchorMinX, $x)
                        $anchorMaxX = [Math]::Max($anchorMaxX, $x)
                    }
                }
            }
            $anchorX = ($anchorMinX + $anchorMaxX) / 2.0
            $targetX = if ($isLong) { 23.5 } else { 7 }
            $targetY = if ($isLong) { 6 } else { 4 }
            $sprite = [System.Drawing.Bitmap]::new(48, $height)
            for ($y = 0; $y -lt $height; $y++) {
                for ($x = 0; $x -lt 48; $x++) {
                    $sx = [int][Math]::Round($anchorX + ($x - $targetX) / $scale)
                    $sy = [int][Math]::Round($capTop + ($y - $targetY) / $scale)
                    if ($sx -ge $left -and $sx -lt $right -and $sy -ge $top -and $sy -lt $bottom) {
                        $pixel = $source.GetPixel($sx, $sy)
                        if ($pixel.A -ge 192) { $sprite.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.G, $pixel.B)) }
                    }
                }
            }
            # Mounting hardware is a static layer, shared across all frames.
            # Cloth animation comes from the individually drawn source frames.
            if ($frame -gt 0) {
                for ($y = 0; $y -lt $height; $y++) {
                    for ($x = 0; $x -lt 48; $x++) {
                        if (($isLong -and $y -lt 15) -or (!$isLong -and $x -lt 10)) {
                            $sprite.SetPixel($x, $y, $frames[0].GetPixel($x, $y))
                        }
                    }
                }
            }
            Save-Png $sprite ("$kind/$($names[$faction])/frame_{0:D2}.png" -f $frame)
            $sg.DrawImageUnscaled($sprite, ($frame * 48), 0)
            $ag.DrawImageUnscaled($sprite, ($frame * 48), ($faction * $height))
            $frames += ,$sprite
        }
        $sg.Dispose()
        Save-Png $strip "$kind/$($names[$faction])_八帧.png"
        $strip.Dispose()
        $allFrames["$kind/$faction"] = $frames
        $manifest.animations += [ordered]@{ name = "$($names[$faction])_$kind"; sheet = "$kind/$($names[$faction])_八帧.png"; frame_size = @(48, $height); frames = 8; fps = $fps; loop = $true; atlas_row = $faction; tile_footprint = @(1, ($height / 48)); anchor_pixel = @($targetX, $targetY) }
    }
    $ag.Dispose()
    Save-Png $atlas "图集/${kind}_五方八帧.png"
    $atlas.Dispose(); $source.Dispose()
}
$motifs = [System.Drawing.Bitmap]::new(240, 48)
$mg = [System.Drawing.Graphics]::FromImage($motifs)
for ($i = 0; $i -lt 5; $i++) {
    $motif = [System.Drawing.Bitmap]::new((Join-Path $root "参考纹章/$($names[$i])_48px.png"))
    Save-Png $motif "纹样/$($names[$i]).png"
    $mg.DrawImageUnscaled($motif, ($i * 48), 0)
    $motif.Dispose()
}
$mg.Dispose(); Save-Png $motifs '图集/纹样_五方.png'
# Four seconds at 10fps closes both the 6fps and 10fps eight-frame loops.
for ($tick = 0; $tick -lt 40; $tick++) {
    $preview = [System.Drawing.Bitmap]::new(240, 192)
    $g = [System.Drawing.Graphics]::FromImage($preview)
    $g.Clear([System.Drawing.Color]::FromArgb(34, 39, 47))
    $longFrame = [int][Math]::Floor($tick * 0.6) % 8
    for ($i = 0; $i -lt 5; $i++) {
        $g.DrawImageUnscaled($allFrames["长旗/$i"][$longFrame], ($i * 48), 0)
        $g.DrawImageUnscaled($allFrames["横旗/$i"][$tick % 8], ($i * 48), 96)
    }
    $g.DrawImageUnscaled($motifs, 0, 144)
    $g.Dispose()
    $zoom = Resize-Pixel $preview 960 768
    Save-Png $zoom ('预览/合成帧/frame_{0:D2}.png' -f $tick)
    if ($tick -eq 0) { Save-Png $zoom '预览/尺寸与留白.png' }
    $zoom.Dispose(); $preview.Dispose()
}
$motifs.Dispose()
foreach ($frames in $allFrames.Values) { foreach ($frame in $frames) { $frame.Dispose() } }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root '图集/animations.json') -Encoding utf8
Write-Output '打包完成：80 个动画帧，5 个静态纹样。'


