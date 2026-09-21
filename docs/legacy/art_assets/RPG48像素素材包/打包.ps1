Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
foreach ($folder in @('旗帜', '纹章', '图集', '标题背景', '预览', '48px切片')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root $folder) | Out-Null
}
function Save-Png($bitmap, $relative) {
    $bitmap.Save((Join-Path $root $relative), [System.Drawing.Imaging.ImageFormat]::Png)
}
function Resize-Pixel($source, [int]$width, [int]$height) {
    $result = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $width, $height), 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    return ,$result
}
$source = [System.Drawing.Bitmap]::new((Join-Path $root '源图/五方旗帜生成原图.png'))
$atlas = [System.Drawing.Bitmap]::new(480, 432)
$atlasGraphics = [System.Drawing.Graphics]::FromImage($atlas)
$names = @('皇室团龙', '皇家科学院', '神秘学公会', '学院', '伯爵个人')
$columns = @(32, 318, 582, 838, 1106, 1384)
$rowStarts = @(16, 499, 807)
$rowEnds = @(499, 807, 1122)
$heights = @(192, 144, 96)
$atlasRows = @(0, 192, 336)
$types = @('长条旗', '旌旗', '镂空纹章')
$entries = @()
for ($row = 0; $row -lt 3; $row++) {
    for ($col = 0; $col -lt 5; $col++) {
        $left = $columns[$col]; $right = $columns[$col + 1]
        $top = $rowStarts[$row]; $bottom = $rowEnds[$row]
        $minX = $right; $minY = $bottom; $maxX = $left; $maxY = $top
        for ($y = $top; $y -lt $bottom; $y++) {
            for ($x = $left; $x -lt $right; $x++) {
                if ($source.GetPixel($x, $y).A -ge 128) {
                    $minX = [Math]::Min($x, $minX); $maxX = [Math]::Max($x, $maxX)
                    $minY = [Math]::Min($y, $minY); $maxY = [Math]::Max($y, $maxY)
                }
            }
        }
        $crop = $source.Clone([System.Drawing.Rectangle]::new($minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $scale = [Math]::Min(88.0 / $crop.Width, ($heights[$row] - 16.0) / $crop.Height)
        $scaled = Resize-Pixel $crop ([int][Math]::Floor($crop.Width * $scale)) ([int][Math]::Floor($crop.Height * $scale))
        $sprite = [System.Drawing.Bitmap]::new(96, $heights[$row])
        $offsetX = [int][Math]::Floor((96 - $scaled.Width) / 2)
        for ($y = 0; $y -lt $scaled.Height; $y++) {
            for ($x = 0; $x -lt $scaled.Width; $x++) {
                $pixel = $scaled.GetPixel($x, $y)
                if ($pixel.A -ge 128) {
                    $sprite.SetPixel(($x + $offsetX), ($y + 8), [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.G, $pixel.B))
                }
            }
        }
        $folder = if ($row -eq 2) { '纹章' } else { '旗帜' }
        $file = "$folder/$($names[$col])_$($types[$row]).png"
        Save-Png $sprite $file
        $atlasGraphics.DrawImageUnscaled($sprite, ($col * 96), $atlasRows[$row])
        $entries += [ordered]@{ name = "$($names[$col])_$($types[$row])"; file = $file; atlas_rect = @(($col * 96), $atlasRows[$row], 96, $heights[$row]); tile_origin = @(($col * 2), ($atlasRows[$row] / 48)); tile_size = @(2, ($heights[$row] / 48)) }
        if ($row -eq 2) {
            $small = Resize-Pixel $sprite 48 48
            Save-Png $small "纹章/$($names[$col])_48px.png"
            $small.Dispose()
        }
        $sprite.Dispose(); $scaled.Dispose(); $crop.Dispose()
    }
}
$atlasGraphics.Dispose(); $source.Dispose()
Save-Png $atlas '图集/五方旗帜纹章_48px网格.png'
for ($ty = 0; $ty -lt 9; $ty++) {
    for ($tx = 0; $tx -lt 10; $tx++) {
        $tile = $atlas.Clone([System.Drawing.Rectangle]::new(($tx * 48), ($ty * 48), 48, 48), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        Save-Png $tile ('48px切片/tile_{0:D2}_{1:D2}.png' -f $tx, $ty)
        $tile.Dispose()
    }
}
$preview = Resize-Pixel $atlas 1440 1296
Save-Png $preview '预览/旗帜纹章_3倍.png'
$preview.Dispose(); $atlas.Dispose()
$title = [System.Drawing.Bitmap]::new((Join-Path $root '源图/海岛标题生成原图.png'))
$native = Resize-Pixel $title 768 432
Save-Png $native '标题背景/海岛学院_无字_768x432.png'
$large = Resize-Pixel $native 1536 864
Save-Png $large '标题背景/海岛学院_无字_1536x864.png'
$large.Dispose(); $native.Dispose(); $title.Dispose()
[ordered]@{ tile_size = 48; atlas = '图集/五方旗帜纹章_48px网格.png'; atlas_size = @(480, 432); columns = $names; sprites = $entries } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root '图集/atlas.json') -Encoding utf8
Write-Output '完成：15 个主体、5 个 48px 纹章、90 个网格切片、图集、无字标题背景。'

