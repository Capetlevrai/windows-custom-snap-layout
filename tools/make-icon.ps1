# Génère src\app.ico (icône de l'exe et de l'installeur) : une grande zone bleue + deux petites zones grises,
# le même dessin que l'icône de la zone de notification. Tailles 16 à 256 px, images PNG dans le conteneur ICO.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\app.ico'
$sizes = 16, 24, 32, 48, 64, 128, 256

function Rounded([System.Drawing.Graphics]$g, [System.Drawing.Brush]$b, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); $g.FillPath($b, $p); $p.Dispose()
}

$images = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 32.0
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0x4C, 0xC2, 0xFF))
    $gray = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0xB4, 0xB4, 0xB4))
    Rounded $g $accent (1 * $k) (3 * $k) (17 * $k) (26 * $k) (3 * $k)
    Rounded $g $gray (20 * $k) (3 * $k) (11 * $k) (12 * $k) (3 * $k)
    Rounded $g $gray (20 * $k) (17 * $k) (11 * $k) (12 * $k) (3 * $k)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}

$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Dispose()
Write-Host "Icône : $out"
