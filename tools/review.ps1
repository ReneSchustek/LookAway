#requires -Version 7
<#
.SYNOPSIS
    Lokale Review-Pipeline fuer LookAway: Build, Tests, Security-Scan und
    Report-Skeleton.

.DESCRIPTION
    Wird vom Solution-Root oder aus tools/ aufgerufen. Aktiviert den im
    geforderten Review-Workflow ohne Cloud-Round-Trips.

    Modi:
      build       - dotnet build (TreatWarningsAsErrors greift global)
      test        - build + dotnet test
      security    - sucht nach Secret-Mustern und gefaehrlichen Aufrufen
      all         - build + test + security

.PARAMETER Mode
    build | test | security | all | enterprise

.EXAMPLE
    ./tools/review.ps1 -Mode all

.EXAMPLE
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('build', 'test', 'security', 'all', 'enterprise')]
    [string]$Mode = 'all'
)

$ErrorActionPreference = 'Stop'

# Vom Skript ausgehend zum Solution-Root navigieren.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir
$Solution = Join-Path $SolutionRoot 'LookAway.slnx'

if (-not (Test-Path $Solution)) {
    throw "Solution nicht gefunden: $Solution"
}

function Write-Section {
    param([string]$Title)
    Write-Host ''
    Write-Host ("=" * 72) -ForegroundColor DarkCyan
    Write-Host (" $Title") -ForegroundColor Cyan
    Write-Host ("=" * 72) -ForegroundColor DarkCyan
}

function Invoke-Build {
    Write-Section 'Build (TreatWarningsAsErrors)'
    & dotnet build $Solution --nologo -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build fehlgeschlagen (Exit $LASTEXITCODE)."
    }
}

function Invoke-Tests {
    Write-Section 'Tests'
    & dotnet test $Solution --nologo --no-build -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Tests fehlgeschlagen (Exit $LASTEXITCODE)."
    }
}

function Invoke-Security {
    Write-Section 'Security-Scan (Secrets und gefaehrliche Patterns)'

    $patterns = @(
        @{ Name = 'API-Key';            Pattern = '(api[_-]?key|secret[_-]?key)\s*=\s*["''][^"'']{8,}["'']' },
        @{ Name = 'Hartkodiertes Pwd';  Pattern = 'password\s*=\s*["''][^"'']{4,}["'']' },
        @{ Name = 'Connection-String';  Pattern = '(server|data source)=[^;\s"'']+;\s*(uid|user id|password|pwd)=' },
        @{ Name = 'Console.WriteLine';  Pattern = 'Console\.WriteLine' },
        @{ Name = 'BinaryFormatter';    Pattern = 'BinaryFormatter' },
        @{ Name = 'MD5/SHA1';           Pattern = '\b(MD5|SHA1)\.Create' }
    )

    $sourceDirs = @(
        (Join-Path $SolutionRoot 'src'),
        (Join-Path $SolutionRoot 'tests')
    )

    $hits = @()
    foreach ($entry in $patterns) {
        foreach ($dir in $sourceDirs) {
            if (-not (Test-Path $dir)) { continue }
            $found = Get-ChildItem -Path $dir -Recurse -Include *.cs, *.xaml -File |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
                Select-String -Pattern $entry.Pattern -CaseSensitive:$false
            foreach ($match in $found) {
                $hits += [PSCustomObject]@{
                    Pattern = $entry.Name
                    File    = $match.Path.Substring($SolutionRoot.Length).TrimStart('\', '/')
                    Line    = $match.LineNumber
                    Snippet = $match.Line.Trim()
                }
            }
        }
    }

    if ($hits.Count -eq 0) {
        Write-Host 'Keine verdaechtigen Patterns gefunden.' -ForegroundColor Green
    }
    else {
        $hits | Format-Table Pattern, File, Line, Snippet -AutoSize | Out-String | Write-Host
        Write-Host "Treffer: $($hits.Count)" -ForegroundColor Yellow
    }

    return $hits
}

function New-EnterpriseReportSkeleton {
    param([array]$SecurityHits)

    $reviewDir = Join-Path $SolutionRoot '.ai\reviews'
    if (-not (Test-Path $reviewDir)) {
        New-Item -ItemType Directory -Path $reviewDir -Force | Out-Null
    }

    $datestamp = (Get-Date).ToString('yyyy-MM-dd-HHmmss')
    $reportPath = Join-Path $reviewDir "review-$datestamp-enterprise.md"

    $secCount = if ($SecurityHits) { $SecurityHits.Count } else { 0 }
    $secSummary = if ($secCount -eq 0) {
        'Keine verdaechtigen Patterns durch automatischen Scan gefunden.'
    }
    else {
        "Automatischer Scan hat $secCount Treffer gefunden — siehe Tabelle unten."
    }

    $hitsTable = ''
    if ($secCount -gt 0) {
        $hitsTable = "`n| Pattern | Datei | Zeile |`n|---|---|---|`n"
        foreach ($h in $SecurityHits) {
            $hitsTable += "| $($h.Pattern) | $($h.File) | $($h.Line) |`n"
        }
    }

    $template = @"
# Review-Report: LookAway

> Stand: $((Get-Date).ToString('yyyy-MM-dd'))
> Reviewer: Architektur-Review (manuell) + Ollama (Pattern-Scans)
> Scope: gesamter Solution-Stand zum Stichtag

## Zusammenfassung
<3-5 Zeilen — was ist der Stand>

## Vorabscans (review.ps1)
- Build mit ``TreatWarningsAsErrors``: gruen (siehe Konsole)
- Tests: gruen (siehe Konsole)
- Security: $secSummary
$hitsTable
## Saeulen-Audit

### Resilienz
- **Befund:** ...
- **Fehlend:** ...
- **Punkte:** X/10

### Observability
- **Befund:** ...
- **Fehlend:** ...
- **Punkte:** X/10

### Resource Stewardship
- **Befund:** ...
- **Fehlend:** ...
- **Punkte:** X/10

## Bewertungs-Tabelle

| Dimension | Gewicht | Punkte (0-10) | Beitrag |
|---|---|---|---|
| Architektur-Klarheit | 10% |  |  |
| Resilienz | 15% |  |  |
| Observability | 15% |  |  |
| Resource Stewardship | 10% |  |  |
| Sicherheit | 15% |  |  |
| Test-Substanz | 15% |  |  |
| Operations-Bereitschaft | 10% |  |  |
| Wartbarkeit | 10% |  |  |
| **Gesamt** | **100%** | – |  |

## Production-Readiness-Score: **% — Ampel**

## Top-3 Massnahmen vor GO

1. ...
2. ...
3. ...

## Was nach GO als Tech-Debt akzeptabel ist

- ...

## Prinzipien-Check

| Prinzip | Status | Befund |
|---|---|---|
| KISS | ? |  |
| DRY | ? |  |
| YAGNI | ? |  |
| SOLID | ? |  |
| POLS | ? |  |
| TDA | ? |  |
| SoC | ? |  |
| Code for the next person | ? |  |
| FCoI | ? |  |
| LoD | ? |  |
| CoC | ? |  |
| TDD | ? |  |
| Guard Clauses | ? |  |
| Aussagekraeftige Namen | ? |  |
| Stepdown Rule | ? |  |
"@

    Set-Content -Path $reportPath -Value $template -Encoding utf8
    Write-Host "Report-Skeleton geschrieben: $reportPath" -ForegroundColor Green
    return $reportPath
}

switch ($Mode) {
    'build' {
        Invoke-Build
    }
    'test' {
        Invoke-Build
        Invoke-Tests
    }
    'security' {
        $null = Invoke-Security
    }
    'all' {
        Invoke-Build
        Invoke-Tests
        $null = Invoke-Security
    }
    'enterprise' {
        Invoke-Build
        Invoke-Tests
        $hits = Invoke-Security
        Write-Section 'Enterprise-Report-Skeleton'
        $null = New-EnterpriseReportSkeleton -SecurityHits $hits
    }
}

Write-Host ''
Write-Host "Review abgeschlossen ($Mode)." -ForegroundColor Green
