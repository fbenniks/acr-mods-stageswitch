# Writes src\StageSwitch.ico: the app logo (orange circle with a white steering wheel, as drawn by AppIcon.Create
# in MainForm.cs) at 16-256 px, so the exe and shortcuts to it show the logo. build.ps1 embeds it.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$out = Join-Path $root 'src\StageSwitch.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function Draw([int]$n) {
    $bmp = New-Object Drawing.Bitmap $n, $n, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $k = $n / 32.0
    $b = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 122, 26))
    $g.FillEllipse($b, 1 * $k, 1 * $k, 30 * $k, 30 * $k)
    $pen = New-Object Drawing.Pen ([Drawing.Color]::White), ([Math]::Max(1.5, 3 * $k))
    $g.DrawEllipse($pen, 7 * $k, 7 * $k, 18 * $k, 18 * $k)
    $g.DrawLine($pen, 16 * $k, 16 * $k, 16 * $k, 25 * $k)
    $g.DrawLine($pen, 8 * $k, 14 * $k, 24 * $k, 14 * $k)
    $g.Dispose(); $b.Dispose(); $pen.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}

# ICO file: header, one directory entry per size, then the PNG images
$images = foreach ($s in $sizes) { , (Draw $s) }
$fs = [IO.File]::Create($out)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close()
Write-Host "Wrote $out"
