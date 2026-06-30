<#
.SYNOPSIS
    Baut die LookAway-Setup.exe: self-contained Publish + Inno-Setup-Kompilierung.

.DESCRIPTION
    1. Publiziert LookAway.App self-contained (.NET + Windows App SDK) nach
       dist\setup-publish — eine lauffaehige, unpackaged App ohne externe Runtime.
    2. Erzeugt das Setup-Icon (DIB-ICO) und kompiliert installer\LookAway.iss mit
       Inno Setup zur Setup.exe.

    Ergebnis: <OutputDir>\LookAway-Setup-v<Version>.exe

.PARAMETER Version
    Versionsnummer (Default: 1.1.0).

.PARAMETER OutputDir
    Zielordner fuer die fertige Setup.exe (Default: dist/ im Repo-Root).

.PARAMETER Runtime
    Ziel-RID (Default: win-x64).
#>
param(
    [string]$Version = '1.1.0',
    [string]$OutputDir = "$PSScriptRoot\..\dist",
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root 'src\LookAway.App\LookAway.App.csproj'
$publishDir = Join-Path $root 'dist\setup-publish'
$iss = Join-Path $root 'installer\LookAway.iss'

# Inno-Setup-Compiler finden
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) nicht gefunden. Installation: winget install JRSoftware.InnoSetup' }

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Force $OutputDir | Out-Null }

Write-Host "Publish (self-contained .NET + Windows App SDK, $Runtime) ..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
& dotnet publish $appProject `
    -c Release `
    -r $Runtime `
    --self-contained `
    -p:Platform=x64 `
    -p:Version=$Version `
    -p:WindowsAppSDKSelfContained=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen ($LASTEXITCODE)." }

Write-Host 'Setup-Icon erzeugen ...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'make-setup-icon.ps1')

Write-Host 'Setup.exe kompilieren (Inno Setup) ...' -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$Version" "/O$OutputDir" $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC fehlgeschlagen ($LASTEXITCODE)." }

$setup = Join-Path $OutputDir "LookAway-Setup-v$Version.exe"
Write-Host ("Fertig: {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)) -ForegroundColor Green
