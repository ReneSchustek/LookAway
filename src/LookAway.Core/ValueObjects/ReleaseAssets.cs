namespace LookAway.Core.ValueObjects;

/// <summary>
/// Die Download-Adressen eines Releases, so wie der Release-Dienst sie liefert: das portable Paket
/// und das Setup, jeweils mit ihrer losgelösten Signatur.
/// </summary>
/// <param name="Package">Portable-ZIP; Grundlage des Datei-Tauschs einer portablen Kopie.</param>
/// <param name="PackageSignature">Signatur des Portable-ZIP (<c>&lt;paket&gt;.sig</c>).</param>
/// <param name="Setup">Setup; Grundlage der Aktualisierung einer installierten Kopie.</param>
/// <param name="SetupSignature">Signatur des Setups (<c>&lt;setup&gt;.sig</c>).</param>
public sealed record ReleaseAssets(string? Package, string? PackageSignature, string? Setup = null, string? SetupSignature = null)
{
    /// <summary>Ein Release ohne verwertbare Downloads.</summary>
    public static ReleaseAssets None { get; } = new(null, null);
}
