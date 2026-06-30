<#
.SYNOPSIS
    Erzeugt die Verteil-Artefakte von LookAway: ein Self-Contained-
    Publish und eine portable ZIP-Variante.

.DESCRIPTION
    Führt 'dotnet publish' für LookAway.App (Self-Contained, win-x64) aus,
    legt die Portable-Markierung 'portable.flag' an und packt das Ergebnis als
    'LookAway-Portable-v<Version>.zip' nach dist/.

    Das MSIX-Paket wird nicht hier erzeugt: Es entsteht über die
    Windows-Packaging-Tools (Visual Studio: "Paket erstellen" oder
    'msbuild /p:WindowsPackageType=MSIX /p:GenerateAppxPackageOnBuild=true').
    Ein signiertes MSIX benötigt ein Code-Signing-Zertifikat (Self-Signed für
    Tests genügt).

.PARAMETER Version
    Versionsnummer der Artefakte (Default: 1.0.0).

.PARAMETER Runtime
    Ziel-RID (Default: win-x64).
#>
param(
    [string]$Version = "1.0.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src/LookAway.App/LookAway.App.csproj"
$distDir = Join-Path $root "dist"
$publishDir = Join-Path $distDir "publish-$Runtime"

New-Item -ItemType Directory -Force $distDir | Out-Null
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

Write-Host "Publish (Self-Contained, $Runtime) ..." -ForegroundColor Cyan
& dotnet publish $appProject `
    -c Release `
    -r $Runtime `
    --self-contained `
    -p:Platform=x64 `
    -p:Version=$Version `
    -p:WindowsAppSDKSelfContained=true `
    -o $publishDir

# Portable-Markierung: lässt die App ihre Daten neben der EXE ablegen.
New-Item -ItemType File -Force (Join-Path $publishDir "portable.flag") | Out-Null

$zipPath = Join-Path $distDir "LookAway-Portable-v$Version.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

Write-Host "Packe portable ZIP ..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "Fertig: $zipPath" -ForegroundColor Green
