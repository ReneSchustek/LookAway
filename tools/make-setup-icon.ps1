# Erzeugt installer/setup-icon.ico (DIB-Frames) aus dem LookAway-Logo —
# Inno Setup verlangt ein klassisches ICO (keine PNG-komprimierten Frames).
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$src = 'F:\Entwicklung\_Icons\LookAway.png'
if (-not (Test-Path $src)) { $src = Join-Path $repo 'src\LookAway.App\Assets\LookAwayLogo.png' }
$out = Join-Path $repo 'installer\setup-icon.ico'
$sizes = @(16, 24, 32, 48, 64)

function Get-FrameBytes {
    param([System.Drawing.Image]$Src, [int]$Size)
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($Src, 0, 0, $Size, $Size); $g.Dispose()
    $rect = New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $buf = New-Object byte[] ($stride * $Size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $buf.Length)
    $bmp.UnlockBits($data); $bmp.Dispose()
    $ms = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([UInt32]40); $bw.Write([Int32]$Size); $bw.Write([Int32]($Size * 2))
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
    $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
    for ($y = $Size - 1; $y -ge 0; $y--) { $bw.Write($buf, $y * $stride, $Size * 4) }
    $andRow = [int][math]::Floor(($Size + 31) / 32) * 4
    $zero = New-Object byte[] $andRow
    for ($y = 0; $y -lt $Size; $y++) { $bw.Write($zero, 0, $andRow) }
    $bw.Flush(); $bytes = $ms.ToArray(); $bw.Dispose(); $ms.Dispose(); return , $bytes
}

$img = [System.Drawing.Image]::FromFile($src)
$frames = @(); foreach ($s in $sizes) { $frames += , @{ Size = $s; Bytes = (Get-FrameBytes -Src $img -Size $s) } }
$img.Dispose()

$o = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter($o)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $bw.Write([byte]$f.Size); $bw.Write([byte]$f.Size); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$f.Bytes.Length); $bw.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $bw.Write($f.Bytes) }
$bw.Flush(); [System.IO.File]::WriteAllBytes($out, $o.ToArray()); $bw.Dispose(); $o.Dispose()

# Selbsttest: exakt wie Inno/H.NotifyIcon -> System.Drawing.Icon(stream, size)
$fs = [System.IO.File]::OpenRead($out)
try { $i = New-Object System.Drawing.Icon($fs, (New-Object System.Drawing.Size(32, 32))); Write-Output ("setup-icon.ico OK ({0} Bytes, {1}x{2})" -f (Get-Item $out).Length, $i.Width, $i.Height); $i.Dispose() }
finally { $fs.Dispose() }
