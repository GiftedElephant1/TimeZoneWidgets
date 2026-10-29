# Generates assets\app.ico (a clock face over a globe) with nothing but System.Drawing.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outDir = Join-Path $root "assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outFile = Join-Path $outDir "app.ico"

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 32.0

    # Globe
    $ocean = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 99, 214))
    $g.FillEllipse($ocean, 1 * $s, 1 * $s, 30 * $s, 30 * $s)
    $land = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 96, 190, 110))
    $g.FillEllipse($land, 6 * $s, 5 * $s, 9 * $s, 8 * $s)
    $g.FillEllipse($land, 17 * $s, 8 * $s, 10 * $s, 7 * $s)
    $g.FillEllipse($land, 9 * $s, 17 * $s, 7 * $s, 9 * $s)
    $g.FillEllipse($land, 19 * $s, 18 * $s, 8 * $s, 6 * $s)
    # Night shading on the right
    $shade = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(110, 0, 0, 30))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(1 * $s, 1 * $s, 30 * $s, 30 * $s, -90, 180)
    $path.AddArc(9 * $s, 1 * $s, 14 * $s, 30 * $s, 90, -180)
    $g.FillPath($shade, $path)

    # Clock hands
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([float](2.6 * $s))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, 16 * $s, 16 * $s, 16 * $s, 7.5 * $s)
    $g.DrawLine($pen, 16 * $s, 16 * $s, 22.5 * $s, 19.5 * $s)
    $ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 255, 255)), ([float](1.4 * $s))
    $g.DrawEllipse($ring, 1.2 * $s, 1.2 * $s, 29.6 * $s, 29.6 * $s)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    # Add to the list directly: returning a byte[] from a function would unroll it into the pipeline.
    $script:images.Add([byte[]]$ms.ToArray())
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$script:images = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($sz in $sizes) { Draw-Icon $sz }

# ICO container: header, one directory entry per image, then PNG payloads.
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $writer.Write($img) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($outFile, $stream.ToArray())
Write-Host "Wrote $outFile"
