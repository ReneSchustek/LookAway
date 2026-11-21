<#
.SYNOPSIS
    Führt die Messungen aus tests/LookAway.Benchmarks aus und legt die Werte je Lauf ab.
.DESCRIPTION
    Misst mit BenchmarkDotNet im Release-Bau: Historie und Statistik über ein Jahr, die Protokollseite
    über eine Woche und das Prüfen eines Overlay-Fotos vor dem Pausenbeginn. Der Messbestand entsteht
    aus festen Startwerten (tests/MeasurementData) und ist auf jedem Rechner derselbe.

    Messwerte gehören zu einem Rechner; Zahlen verschiedener Rechner sind nicht vergleichbar. Die
    Ablage liegt deshalb außerhalb des Repositorys und wird mit -ResultsDir angegeben. Je Lauf
    entsteht ein Ordner <Datum>_<Uhrzeit>-<Rechner> mit den Markdown-Berichten von BenchmarkDotNet
    (sie nennen auch Prozessor, Windows und .NET) und einer Kopfdatei mit Fassung und Commit. Erst
    die Reihe der Läufe zeigt, ob etwas langsamer wird.
.PARAMETER ResultsDir
    Ordner der Ablage; je Lauf entsteht darin ein Unterordner.
.PARAMETER Filter
    Filter von BenchmarkDotNet, etwa '*History*'. Standard: alle Messungen.
.EXAMPLE
    ./tools/run-benchmarks.ps1 -ResultsDir D:\Messungen\LookAway
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResultsDir,
    [string]$Filter = '*'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path "$PSScriptRoot\..").Path
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$artifacts = Join-Path ([IO.Path]::GetTempPath()) "lookaway-benchmarks-$stamp"

dotnet run -c Release --project "$repo\tests\LookAway.Benchmarks" -- --filter $Filter --artifacts $artifacts
if ($LASTEXITCODE -ne 0) { throw "Messung fehlgeschlagen (Exitcode $LASTEXITCODE)." }

$reports = Get-ChildItem -Path (Join-Path $artifacts 'results') -Filter '*-report-github.md'
if (-not $reports) { throw "Keine Berichte unter $artifacts — der Filter '$Filter' hat keine Messung getroffen." }

$version = (Select-Xml -Path "$repo\Directory.Build.props" -XPath '//Version').Node.InnerText
$commit = (git -C $repo rev-parse --short HEAD).Trim()
# Ein Lauf auf geändertem Stand muss als solcher erkennbar sein, sonst ordnet man ihm den Commit zu.
$changed = if (git -C $repo status --porcelain) { ' (mit lokalen Änderungen)' } else { '' }

$target = Join-Path $ResultsDir "$stamp-$env:COMPUTERNAME"
New-Item -ItemType Directory -Force $target | Out-Null
$reports | Copy-Item -Destination $target
@(
    "# Messlauf $stamp",
    '',
    "- Fassung: $version, Commit $commit$changed",
    "- Rechner: $env:COMPUTERNAME",
    "- Filter: $Filter",
    "- Berichte: $($reports.Count)"
) | Set-Content -LiteralPath (Join-Path $target 'messlauf.md') -Encoding utf8

Remove-Item -LiteralPath $artifacts -Recurse -Force
Write-Host "Messwerte abgelegt: $target" -ForegroundColor Green
