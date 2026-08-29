# Generates the app artwork into assets/:
#   PhotoDrop.ico        - the exe and tray icon (8 sizes, DIB below 256px)
#   icon-192.png         - web manifest icon (Android home screen)
#   icon-512.png         - web manifest icon / splash
#   apple-touch-icon.png - iOS home screen: full-bleed and opaque, because iOS
#                          applies its own rounded mask and paints transparency black
#
# A blue rounded square with a "save to device" arrow. Each size is drawn natively
# rather than scaled, so 16px stays crisp.
# Run: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1

Add-Type -AssemblyName System.Drawing

$icoSizes = 16, 20, 24, 32, 48, 64, 128, 256
$outDir   = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets'
$outIco   = Join-Path $outDir 'PhotoDrop.ico'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function New-RoundedRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x,             $y,             $d, $d, 180, 90)
    $p.AddArc($x + $w - $d,   $y,             $d, $d, 270, 90)
    $p.AddArc($x + $w - $d,   $y + $h - $d,   $d, $d,   0, 90)
    $p.AddArc($x,             $y + $h - $d,   $d, $d,  90, 90)
    $p.CloseFigure()
    return $p
}

# $fullBleed fills the whole square (no rounding, no transparent corners) for iOS.
function New-IconBitmap([int]$s, [switch]$fullBleed) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point($s, $s)),
        [System.Drawing.Color]::FromArgb(255, 91, 156, 255),
        [System.Drawing.Color]::FromArgb(255, 37, 99, 235))

    if ($fullBleed) {
        $g.FillRectangle($brush, 0, 0, $s, $s)
    }
    else {
        $pad  = [single]($s * 0.03)
        $side = [single]($s - $pad * 2)
        $bg   = New-RoundedRect $pad $pad $side $side ([single]($s * 0.22))
        $g.FillPath($brush, $bg)
        $bg.Dispose()
    }

    # Glyph: a downward arrow landing on a bar - "photos land on this PC".
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

    $shaftW = [single]($s * 0.14)
    $shaft  = New-RoundedRect ([single](($s - $shaftW) / 2)) ([single]($s * 0.22)) `
                              $shaftW ([single]($s * 0.34)) ([single][Math]::Max(1, $s * 0.05))
    $g.FillPath($white, $shaft)

    $head = New-Object System.Drawing.Drawing2D.GraphicsPath
    $head.AddPolygon(@(
        (New-Object System.Drawing.PointF([single]($s * 0.27), [single]($s * 0.48))),
        (New-Object System.Drawing.PointF([single]($s * 0.73), [single]($s * 0.48))),
        (New-Object System.Drawing.PointF([single]($s * 0.50), [single]($s * 0.74)))))
    $g.FillPath($white, $head)

    $bar = New-RoundedRect ([single]($s * 0.24)) ([single]($s * 0.79)) `
                           ([single]($s * 0.52)) ([single]($s * 0.09)) `
                           ([single][Math]::Max(1, $s * 0.04))
    $g.FillPath($white, $bar)

    $g.Dispose(); $brush.Dispose(); $white.Dispose()
    $shaft.Dispose(); $head.Dispose(); $bar.Dispose()
    return $bmp
}

# --- the .ico -------------------------------------------------------------------

$entries = @()

foreach ($s in $icoSizes) {
    $bmp = New-IconBitmap $s

    # NotifyIcon/GDI+ only understand PNG-compressed entries at 256px; every
    # smaller size has to be a classic BITMAPINFOHEADER + BGRA + AND-mask blob.
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $payload = $ms.ToArray()
        $ms.Dispose()
    }
    else {
        $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                              [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $rowBytes = $s * 4
        $pixels = New-Object byte[] ($rowBytes * $s)
        for ($row = 0; $row -lt $s; $row++) {
            # DIBs are bottom-up, so copy the source rows in reverse.
            $src = [IntPtr]::Add($data.Scan0, $data.Stride * ($s - 1 - $row))
            [System.Runtime.InteropServices.Marshal]::Copy($src, $pixels, $row * $rowBytes, $rowBytes)
        }
        $bmp.UnlockBits($data)

        $maskRow  = [Math]::Floor(($s + 31) / 32) * 4     # 1bpp, rows padded to 4 bytes
        $mask     = New-Object byte[] ($maskRow * $s)     # all zero = fully opaque
        $dib      = New-Object System.IO.MemoryStream
        $dw       = New-Object System.IO.BinaryWriter($dib)
        $dw.Write([uint32]40)                 # BITMAPINFOHEADER size
        $dw.Write([int32]$s)                  # width
        $dw.Write([int32]($s * 2))            # height = image + mask
        $dw.Write([uint16]1)                  # planes
        $dw.Write([uint16]32)                 # bits per pixel
        $dw.Write([uint32]0)                  # BI_RGB
        $dw.Write([uint32]($pixels.Length + $mask.Length))
        $dw.Write([int32]0); $dw.Write([int32]0)          # pixels-per-metre
        $dw.Write([uint32]0); $dw.Write([uint32]0)        # palette
        $dw.Write($pixels)
        $dw.Write($mask)
        $dw.Flush()
        $payload = $dib.ToArray()
        $dw.Dispose(); $dib.Dispose()
    }
    $entries += ,@($s, $payload)
    $bmp.Dispose()
}

# Assemble the .ico container: 6-byte header, 16 bytes per entry, then the payloads.
$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)

$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type: icon
$bw.Write([uint16]$entries.Count)

$offset = 6 + 16 * $entries.Count
foreach ($entry in $entries) {
    $size = $entry[0]; $data = $entry[1]
    $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))   # width  (0 means 256)
    $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))   # height
    $bw.Write([byte]0)            # palette size
    $bw.Write([byte]0)            # reserved
    $bw.Write([uint16]1)          # colour planes
    $bw.Write([uint16]32)         # bits per pixel
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($entry in $entries) { $bw.Write($entry[1]) }

$bw.Flush()
[System.IO.File]::WriteAllBytes($outIco, $out.ToArray())
$bw.Dispose(); $out.Dispose()
Write-Output "Wrote $outIco ($([Math]::Round((Get-Item $outIco).Length / 1KB, 1)) KB, $($entries.Count) sizes)"

# --- the web icons --------------------------------------------------------------

foreach ($spec in @(@(192, 'icon-192.png', $false), @(512, 'icon-512.png', $false),
                    @(180, 'apple-touch-icon.png', $true))) {
    $size = $spec[0]; $name = $spec[1]; $bleed = $spec[2]
    $bmp  = if ($bleed) { New-IconBitmap $size -fullBleed } else { New-IconBitmap $size }
    $path = Join-Path $outDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "Wrote $path ($([Math]::Round((Get-Item $path).Length / 1KB, 1)) KB)"
}
