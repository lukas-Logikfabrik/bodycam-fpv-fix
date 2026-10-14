<#
  Draws assets\app.ico: a quadcopter seen from above with a red recording light, in the dark style of the app.
  Every size is drawn on its own so small icons stay sharp. Run it again after changing the design:
    powershell -ExecutionPolicy Bypass -File assets\make-icon.ps1
#>
param([string]$Out = (Join-Path $PSScriptRoot 'app.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Draw-Icon([int]$s) {
    $bmp = New-Object Drawing.Bitmap $s, $s, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([Drawing.Color]::Transparent)

    # Rounded dark tile
    $inset = [Math]::Max(0.5, $s * 0.02)
    $size = $s - 2 * $inset
    $radius = $size * 0.22
    $tile = New-Object Drawing.Drawing2D.GraphicsPath
    $tile.AddArc($inset, $inset, 2 * $radius, 2 * $radius, 180, 90)
    $tile.AddArc($inset + $size - 2 * $radius, $inset, 2 * $radius, 2 * $radius, 270, 90)
    $tile.AddArc($inset + $size - 2 * $radius, $inset + $size - 2 * $radius, 2 * $radius, 2 * $radius, 0, 90)
    $tile.AddArc($inset, $inset + $size - 2 * $radius, 2 * $radius, 2 * $radius, 90, 90)
    $tile.CloseFigure()
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.PointF]::new(0, 0)), ([Drawing.PointF]::new(0, $s)), ([Drawing.Color]::FromArgb(255, 40, 41, 43)), ([Drawing.Color]::FromArgb(255, 14, 15, 16))
    $g.FillPath($fill, $tile)
    if ($s -ge 32) {
        $edge = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(150, 115, 117, 117)), ([Math]::Max(1, $s / 64))
        $g.DrawPath($edge, $tile)
        $edge.Dispose()
    }

    # Quadcopter: X arms, four rotors, body with a red light
    $light = [Drawing.Color]::FromArgb(255, 236, 236, 232)
    $c = $s / 2
    $d = $s * 0.25
    $rotor = $s * 0.145
    $arm = New-Object Drawing.Pen $light, ([Math]::Max(1.4, $s * 0.065))
    $reach = $d - $rotor / [Math]::Sqrt(2)   # arms end at the rotor ring, not in its middle
    foreach ($dx in -1, 1) { foreach ($dy in -1, 1) { $g.DrawLine($arm, $c, $c, $c + $dx * $reach, $c + $dy * $reach) } }
    $ring = New-Object Drawing.Pen $light, ([Math]::Max(1.2, $s * 0.05))
    $solid = New-Object Drawing.SolidBrush $light
    foreach ($dx in -1, 1) {
        foreach ($dy in -1, 1) {
            $x = $c + $dx * $d; $y = $c + $dy * $d
            if ($s -le 24) { $g.FillEllipse($solid, $x - $rotor, $y - $rotor, 2 * $rotor, 2 * $rotor) }
            else { $g.DrawEllipse($ring, $x - $rotor, $y - $rotor, 2 * $rotor, 2 * $rotor) }
        }
    }
    $body = $s * 0.13
    $g.FillEllipse($solid, $c - $body, $c - $body, 2 * $body, 2 * $body)
    $red = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 229, 57, 53))
    $dot = if ($s -le 24) { $body * 0.75 } else { $body * 0.62 }
    $g.FillEllipse($red, $c - $dot, $c - $dot, 2 * $dot, 2 * $dot)

    foreach ($o in $arm, $ring, $solid, $red, $fill, $tile, $g) { $o.Dispose() }
    return $bmp
}

# 32-bit DIB entry as stored in .ico files: header with double height, bottom-up BGRA rows, empty AND mask
function Get-DibBytes([Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
    $data = $bmp.LockBits((New-Object Drawing.Rectangle 0, 0, $s, $s), 'ReadOnly', ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $row = New-Object byte[] ($s * 4)
    for ($y = $s - 1; $y -ge 0; $y--) {
        [Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $s * 4)
        $w.Write($row)
    }
    $bmp.UnlockBits($data)
    $w.Write((New-Object byte[] ([Math]::Ceiling($s / 32) * 4 * $s)))
    $w.Flush()
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    if ($s -eq 256) { $ms = New-Object IO.MemoryStream; $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bytes = $ms.ToArray() }
    else { $bytes = Get-DibBytes $bmp }
    $bmp.Dispose()
    ,@($s, $bytes)
}

$file = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $file
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img[0] -ge 256) { 0 } else { $img[0] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$img[1].Length); $w.Write([int]$offset)
    $offset += $img[1].Length
}
foreach ($img in $images) { $w.Write($img[1]) }
$w.Flush()
[IO.File]::WriteAllBytes($Out, $file.ToArray())
Write-Host "Wrote $Out ($($file.Length) bytes, sizes $($sizes -join ', '))"
