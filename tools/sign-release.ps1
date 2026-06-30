<#
.SYNOPSIS
    Signiert ein Release-Paket (Portable-ZIP) mit dem privaten Release-Schlüssel und
    schreibt die losgelöste Signatur als <Paket>.sig daneben.

.DESCRIPTION
    Der Auto-Updater von LookAway lädt zusätzlich zum Paket dessen Signatur und prüft
    sie vor dem Einspielen gegen den eingebetteten öffentlichen Schlüssel
    (ReleaseSignatureVerifier, fail-closed). Dieses Skript erzeugt genau diese Signatur:

      * Verfahren: ECDSA P-256 über SHA-256 der gesamten Paketdatei (IEEE-P1363-Format).
      * Ausgabe: rohe Signaturbytes in <Paket>.sig (bzw. -OutPath).

    Zur Sicherheit wird die erzeugte Signatur sofort gegen den öffentlichen Schlüssel
    gegengeprüft; schlägt das fehl, bricht das Skript ab. Der ausgegebene öffentliche
    Schlüssel sollte mit ReleaseSignatureVerifier.DefaultPublicKeySpkiBase64 übereinstimmen.

.PARAMETER PackagePath
    Pfad des zu signierenden Pakets (z. B. dist/LookAway-Portable-v1.2.0.zip).

.PARAMETER KeyPath
    Pfad des privaten Schlüssels (PKCS#8-PEM), siehe new-signing-key.ps1.

.PARAMETER OutPath
    Zielpfad der Signatur. Standard: <PackagePath>.sig

.EXAMPLE
    ./tools/sign-release.ps1 -PackagePath dist/LookAway-Portable-v1.2.0.zip `
                             -KeyPath F:\Entwicklung\LookAway-signing\private-key.pem
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$KeyPath,
    [string]$OutPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackagePath)) { throw "Paket nicht gefunden: $PackagePath" }
if (-not (Test-Path -LiteralPath $KeyPath))     { throw "Schlüssel nicht gefunden: $KeyPath" }
if (-not $OutPath) { $OutPath = "$PackagePath.sig" }

# Privaten Schlüssel laden.
$ecdsa = [System.Security.Cryptography.ECDsa]::Create()
try {
    $ecdsa.ImportFromPem((Get-Content -LiteralPath $KeyPath -Raw))

    # Gesamte Paketdatei mit SHA-256 signieren (gleiches Verfahren wie die Prüfung im Client).
    $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $PackagePath))
    $signature = $ecdsa.SignData($bytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256)

    # Selbstkontrolle: ohne gültige Gegenprüfung keine Auslieferung.
    if (-not $ecdsa.VerifyData($bytes, $signature, [System.Security.Cryptography.HashAlgorithmName]::SHA256)) {
        throw 'Selbstprüfung der erzeugten Signatur fehlgeschlagen.'
    }

    [System.IO.File]::WriteAllBytes($OutPath, $signature)
    $publicSpki = [Convert]::ToBase64String($ecdsa.ExportSubjectPublicKeyInfo())
}
finally {
    $ecdsa.Dispose()
}

Write-Host "Signatur geschrieben: $OutPath" -ForegroundColor Green
Write-Host "Öffentlicher Schlüssel (muss dem eingebetteten entsprechen):"
Write-Host $publicSpki
