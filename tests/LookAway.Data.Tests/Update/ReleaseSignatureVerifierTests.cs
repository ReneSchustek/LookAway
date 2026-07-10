using System.Security.Cryptography;
using LookAway.Core.ValueObjects;
using LookAway.Data.Update;

namespace LookAway.Data.Tests;

/// <summary>
/// Tests für den <see cref="ReleaseSignatureVerifier"/>: eine über den privaten
/// Schlüssel erzeugte Signatur wird akzeptiert, jede Abweichung (manipulierte Datei,
/// fremder Schlüssel, leere/kaputte Signatur) wird abgewiesen.
/// </summary>
public sealed class ReleaseSignatureVerifierTests : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _file;

    public ReleaseSignatureVerifierTests()
    {
        _file = Path.Combine(Path.GetTempPath(), "lookaway-sig-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(_file, "ein-update-paket-inhalt"u8.ToArray());
    }

    public void Dispose()
    {
        _key.Dispose();
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
            // best-effort
        }
    }

    private ReleaseSignatureVerifier VerifierForOwnKey()
        => new(Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo()));

    private byte[] SignFile()
        => _key.SignData(File.ReadAllBytes(_file), HashAlgorithmName.SHA256);

    [Fact]
    public void VerifyFile_akzeptiert_gültige_Signatur()
    {
        Assert.True(VerifierForOwnKey().VerifyFile(_file, SignFile()));
    }

    [Fact]
    public void VerifyFile_lehnt_manipulierte_Datei_ab()
    {
        byte[] signature = SignFile();
        File.WriteAllBytes(_file, "ein-MANIPULIERTER-inhalt"u8.ToArray());

        Assert.False(VerifierForOwnKey().VerifyFile(_file, signature));
    }

    [Fact]
    public void VerifyFile_lehnt_fremden_Schlüssel_ab()
    {
        // Mit einem anderen Schlüssel signiert -> der eigene öffentliche Schlüssel verwirft.
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] foreignSignature = otherKey.SignData(File.ReadAllBytes(_file), HashAlgorithmName.SHA256);

        Assert.False(VerifierForOwnKey().VerifyFile(_file, foreignSignature));
    }

    [Fact]
    public void VerifyFile_lehnt_leere_Signatur_ab()
    {
        Assert.False(VerifierForOwnKey().VerifyFile(_file, []));
    }

    [Fact]
    public void VerifyFile_lehnt_kaputte_Signaturbytes_ab()
    {
        Assert.False(VerifierForOwnKey().VerifyFile(_file, [0, 1, 2, 3, 4, 5]));
    }

    [Fact]
    public void Konstruktor_akzeptiert_eingebetteten_Produktionsschlüssel()
    {
        // Der eingebettete Standardschlüssel muss ein gültiger SPKI-Base64 sein.
        ReleaseSignatureVerifier verifier = new();

        Assert.False(verifier.VerifyFile(_file, SignFile()));
    }
}
