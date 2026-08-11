<#
.SYNOPSIS
    Nimmt die Oberflächen-Aufnahmen für die READMEs und die Projektseite auf.

.DESCRIPTION
    Startet eine portable Instanz aus einem vorbereiteten Ordner, schaltet die
    Bereiche des Einstellungsfensters über die Windows-Automatisierung durch und
    legt je Bereich eine PNG ab.

    Drei Eigenheiten bestimmen den Ablauf:

    1. Die Anwendung läuft als Einzelinstanz. Ein zweiter Start übergibt an die
       laufende und beendet sich sofort — genau das öffnet aber das
       Einstellungsfenster. Das Skript nutzt diesen Weg, statt das Symbol im
       Infobereich zu bedienen.
    2. Aufgenommen wird über PrintWindow aus dem Fensterpuffer, nicht als
       Bildschirmausschnitt. Sonst landet alles mit im Bild, was zufällig davor
       liegt.
    3. GetWindowRect liefert die Fenstergrenzen samt unsichtbarem Schattenrahmen,
       den PrintWindow schwarz füllt. Abgeschnitten wird er über die tatsächlich
       sichtbaren Grenzen aus der Fensterverwaltung.

    Der Aufnahmeordner sollte eine vorbereitete portable Instanz sein (mit
    portable.flag, settings.json und history.json). Läuft das Skript gegen eine
    Installation mit echten Daten, stehen persönliche Zeiten im Bild.

.PARAMETER SourceDirectory
    Ordner mit der portablen Instanz (LookAway.exe, portable.flag).

.PARAMETER TargetDirectory
    Zielordner für die PNG-Dateien.

.PARAMETER Width
    Zielbreite der abgelegten Bilder in Pixeln; das Seitenverhältnis bleibt
    erhalten. 0 legt die Aufnahme unskaliert ab.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$TargetDirectory,
    [int]$Width = 800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowCapture
{
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    // Die sichtbaren Grenzen des Fensters - ohne den Schattenrahmen, den die
    // Fensterverwaltung um moderne Fenster legt.
    public const int ExtendedFrameBounds = 9;
}
'@

function Save-WindowImage {
    param([IntPtr]$Handle, [string]$Path, [int]$TargetWidth)

    $frame = New-Object WindowCapture+RECT
    [void][WindowCapture]::GetWindowRect($Handle, [ref]$frame)
    $imageWidth = $frame.Right - $frame.Left
    $imageHeight = $frame.Bottom - $frame.Top
    if ($imageWidth -le 0 -or $imageHeight -le 0) {
        throw "Unbrauchbare Fenstermaße: ${imageWidth}x${imageHeight}"
    }

    $image = New-Object System.Drawing.Bitmap($imageWidth, $imageHeight)
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    $deviceContext = $graphics.GetHdc()
    # Flag 2 = PRF_CHILDREN; ohne das bleiben die Steuerelemente leer.
    [void][WindowCapture]::PrintWindow($Handle, $deviceContext, 2)
    $graphics.ReleaseHdc($deviceContext)
    $graphics.Dispose()

    # Schattenrahmen abschneiden. Über die Fensterverwaltung und nicht über die
    # Bildfarbe: Ein Beschnitt nach "dunklen Randzeilen" würde beim Pausen-Screen,
    # der ganzflächig dunkel ist, das halbe Bild wegnehmen.
    $visible = New-Object WindowCapture+RECT
    if ([WindowCapture]::DwmGetWindowAttribute($Handle, [WindowCapture]::ExtendedFrameBounds, [ref]$visible, 16) -eq 0) {
        $left = $visible.Left - $frame.Left
        $top = $visible.Top - $frame.Top
        $cropWidth = $visible.Right - $visible.Left
        $cropHeight = $visible.Bottom - $visible.Top

        if ($left -ge 0 -and $top -ge 0 -and $cropWidth -gt 0 -and $cropHeight -gt 0 -and
            ($left + $cropWidth) -le $imageWidth -and ($top + $cropHeight) -le $imageHeight -and
            ($cropWidth -ne $imageWidth -or $cropHeight -ne $imageHeight)) {

            # Ein Pixel Sicherheitsabstand: Die Grenze verläuft zwischen zwei Pixeln,
            # und beim späteren Skalieren mischt sich der Randpixel sonst als dunkler
            # Strich in das fertige Bild.
            if ($left + $cropWidth + 1 -le $imageWidth) { $left += 1; $cropWidth -= 2 }
            if ($top + $cropHeight + 1 -le $imageHeight) { $top += 1; $cropHeight -= 2 }

            $region = New-Object System.Drawing.Rectangle($left, $top, $cropWidth, $cropHeight)
            $cropped = $image.Clone($region, $image.PixelFormat)
            $image.Dispose()
            $image = $cropped
            $imageWidth = $cropWidth
            $imageHeight = $cropHeight
        }
    }

    if ($TargetWidth -gt 0 -and $TargetWidth -ne $imageWidth) {
        $scaledHeight = [int]([math]::Round($imageHeight * ($TargetWidth / $imageWidth)))
        $scaled = New-Object System.Drawing.Bitmap($TargetWidth, $scaledHeight)
        $scaler = [System.Drawing.Graphics]::FromImage($scaled)
        $scaler.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $scaler.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $scaler.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $scaler.DrawImage($image, 0, 0, $TargetWidth, $scaledHeight)
        $scaler.Dispose()
        $image.Dispose()
        $image = $scaled
    }

    $image.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $size = "{0}x{1}" -f $image.Width, $image.Height
    $image.Dispose()
    return "{0,-22} {1}" -f (Split-Path $Path -Leaf), $size
}

function Get-WindowByTitle {
    param([string]$Title, [int]$Attempts = 10)

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Title)

    foreach ($attempt in 1..$Attempts) {
        $match = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($match) { return $match }
        Start-Sleep -Seconds 2
    }
    return $null
}

$executable = Join-Path $SourceDirectory 'LookAway.exe'
if (-not (Test-Path $executable)) { throw "LookAway.exe fehlt in $SourceDirectory" }
if (-not (Test-Path (Join-Path $SourceDirectory 'portable.flag'))) {
    Write-Warning 'Kein portable.flag — die Instanz schreibt in das Benutzerprofil.'
}

New-Item -ItemType Directory -Force $TargetDirectory | Out-Null

Start-Process $executable | Out-Null
Start-Sleep -Seconds 6
Start-Process $executable | Out-Null          # Zweitstart öffnet die Einstellungen
Start-Sleep -Seconds 6

$window = Get-WindowByTitle -Title 'Einstellungen'
if (-not $window) { throw 'Einstellungsfenster nicht gefunden.' }

$handle = [IntPtr]$window.Current.NativeWindowHandle
[void][WindowCapture]::SetForegroundWindow($handle)
Start-Sleep -Milliseconds 800

# Menüpunkt -> Dateiname. Nur die Bereiche, die etwas zu zeigen haben.
$sections = [ordered]@{
    'Allgemein'    = 'settings'
    'Pausenmodell' = 'break-models'
    'Statistik'    = 'statistics'
    'Hotkeys'      = 'hotkeys'
}

foreach ($section in $sections.Keys) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $section)
    $item = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (-not $item) { Write-Warning "Menüpunkt '$section' nicht gefunden"; continue }

    try {
        $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    catch {
        Write-Warning "Menüpunkt '$section' ließ sich nicht wählen"
        continue
    }

    Start-Sleep -Milliseconds 900
    $path = Join-Path $TargetDirectory "$($sections[$section]).png"
    Write-Output ('  ' + (Save-WindowImage -Handle $handle -Path $path -TargetWidth $Width))
}

Write-Output 'Aufnahmen fertig. Die Anwendung läuft weiter — für das Pausenfenster wird sie gebraucht.'
