<#
.SYNOPSIS
    Prüft, ob die READMEs dieselbe Version nennen wie Directory.Build.props.

.DESCRIPTION
    Die Versionsnummer steht als Single Source of Truth in Directory.Build.props. Die drei
    READMEs wiederholen sie im Abschnitt "Aktuelles Release" als Link auf die Releases-Seite —
    und genau dort ist sie unbemerkt stehen geblieben: Die Dateien nannten v1.1.1, während
    v1.2.8 veröffentlicht war. Eine Angabe, die niemand prüft, veraltet zuverlässig; dieses
    Skript prüft sie.

    Rückgabe: Exit-Code 1, wenn eine README eine abweichende oder gar keine Version nennt.

.PARAMETER Root
    Repository-Wurzel (Default: übergeordneter Ordner dieses Skripts).
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$propsPfad = Join-Path $Root 'Directory.Build.props'
if (-not (Test-Path -LiteralPath $propsPfad)) { throw "Directory.Build.props nicht gefunden: $propsPfad" }

$props = Get-Content -LiteralPath $propsPfad -Raw
$treffer = [regex]::Match($props, '<Version>\s*([^<\s]+)\s*</Version>')
if (-not $treffer.Success) { throw 'Kein <Version>-Element in Directory.Build.props gefunden.' }
$version = $treffer.Groups[1].Value

Write-Host "Version laut Directory.Build.props: $version"

$fehler = @()
foreach ($name in @('README.md', 'README.en.md', 'README.fr.md')) {
    $pfad = Join-Path $Root $name
    if (-not (Test-Path -LiteralPath $pfad)) { continue }

    # Gesucht ist die Release-Angabe der Form **[v1.2.8](…/releases/latest)**.
    $inhalt = Get-Content -LiteralPath $pfad -Raw
    $angabe = [regex]::Match($inhalt, '\[v(\d+\.\d+\.\d+)\]\(https://github\.com/[^)]*/releases/latest\)')

    if (-not $angabe.Success) {
        $fehler += "$name nennt keine Version im Format [v<x.y.z>](…/releases/latest)."
        continue
    }

    if ($angabe.Groups[1].Value -ne $version) {
        $fehler += "$name nennt v$($angabe.Groups[1].Value), erwartet wird v$version."
    }
    else {
        Write-Host "  $name : v$($angabe.Groups[1].Value)" -ForegroundColor Green
    }
}

if ($fehler.Count -gt 0) {
    foreach ($eintrag in $fehler) { Write-Host "  $eintrag" -ForegroundColor Red }
    Write-Host ''
    Write-Host 'Versionsangaben weichen ab — README-Abschnitt "Aktuelles Release" anpassen.' -ForegroundColor Red
    exit 1
}

Write-Host 'Alle READMEs nennen die aktuelle Version.' -ForegroundColor Green

# Ausdrücklich, nicht implizit: Ohne eigenes 'exit' behält der Aufrufer den $LASTEXITCODE
# des vorherigen Befehls — der Aufruf in review.ps1 lief damit trotz grüner Prüfung auf
# einen Fehlschlag.
exit 0
