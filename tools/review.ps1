#requires -Version 7
<#
.SYNOPSIS
    Lokale Review-Pipeline für LookAway: Build, Tests, Security-Scan und
    Report-Skeleton.

.DESCRIPTION
    Wird vom Solution-Root oder aus tools/ aufgerufen. Bündelt die lokalen
    Qualitäts-Checks ohne Cloud-Round-Trips.

    Modi:
      build       - dotnet build (TreatWarningsAsErrors greift global)
      test        - build + dotnet test
      security    - sucht nach Secret-Mustern und gefährlichen Aufrufen
      all         - build + test + security

.PARAMETER Mode
    build | test | security | all

.EXAMPLE
    ./tools/review.ps1 -Mode all

.EXAMPLE
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('build', 'test', 'security', 'all')]
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
    Write-Section 'Security-Scan (Secrets und gefährliche Patterns)'

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
        Write-Host 'Keine verdächtigen Patterns gefunden.' -ForegroundColor Green
    }
    else {
        $hits | Format-Table Pattern, File, Line, Snippet -AutoSize | Out-String | Write-Host
        Write-Host "Treffer: $($hits.Count)" -ForegroundColor Yellow
    }

    return $hits
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
}

Write-Host ''
Write-Host "Review abgeschlossen ($Mode)." -ForegroundColor Green
