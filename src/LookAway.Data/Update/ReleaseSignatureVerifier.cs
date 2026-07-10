using System.Security.Cryptography;

namespace LookAway.Data.Update;

/// <summary>
/// Prüft die Echtheit eines Update-Pakets über eine losgelöste ECDSA-P-256-Signatur
/// (SHA-256). Der öffentliche Schlüssel ist fest eingebettet; der zugehörige private
/// Schlüssel wird offline gehalten und signiert die Release-Pakete. So lässt sich ein
/// untergeschobenes oder manipuliertes Paket abweisen, selbst wenn ein Angreifer den
/// Release-Kanal kontrolliert — denn ohne den privaten Schlüssel entsteht keine
/// gültige Signatur.
/// </summary>
public sealed class ReleaseSignatureVerifier
{
    /// <summary>
    /// Eingebetteter öffentlicher Signaturschlüssel (ECDSA P-256, SubjectPublicKeyInfo,
    /// Base64). Der private Gegenpart liegt ausschließlich offline beim Maintainer.
    /// </summary>
    public const string DefaultPublicKeySpkiBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE4/x+nmfDX9HqqJ6MNeFnblllf3FqEdXHyVeW7gYU+KQHPKAL0YCoChTCl0xZ0ozPFJSZslNDg5bkg5pUwAEr7g==";

    private readonly byte[] _publicKeySpki;

    /// <summary>Erzeugt den Prüfer mit dem eingebetteten Produktionsschlüssel.</summary>
    public ReleaseSignatureVerifier()
        : this(DefaultPublicKeySpkiBase64)
    {
    }

    /// <summary>
    /// Erzeugt den Prüfer mit einem expliziten öffentlichen Schlüssel (für Tests).
    /// </summary>
    /// <param name="publicKeySpkiBase64">Öffentlicher Schlüssel als SPKI-Base64.</param>
    public ReleaseSignatureVerifier(string publicKeySpkiBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeySpkiBase64);
        _publicKeySpki = Convert.FromBase64String(publicKeySpkiBase64);
    }

    /// <summary>
    /// Prüft, ob <paramref name="signature"/> eine gültige Signatur des Inhalts von
    /// <paramref name="filePath"/> unter dem eingebetteten Schlüssel ist.
    /// </summary>
    /// <param name="filePath">Pfad der zu prüfenden Datei (Update-Paket).</param>
    /// <param name="signature">Rohe Signaturbytes (IEEE-P1363-Format, SHA-256).</param>
    /// <returns><see langword="true"/> nur bei gültiger Signatur.</returns>
    public bool VerifyFile(string filePath, byte[] signature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(signature);

        if (signature.Length == 0)
        {
            return false;
        }

        try
        {
            using ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(_publicKeySpki, out _);

            using FileStream stream = File.OpenRead(filePath);
            return ecdsa.VerifyData(stream, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
