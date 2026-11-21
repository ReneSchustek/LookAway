using LookAway.App.Services;
using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Seite „Über“: Version, Dokumentation und Spende.
/// </summary>
internal sealed partial class SettingsViewModel
{
    /// <summary>Anzuzeigende Versionsnummer.</summary>
    public string Version => _applicationVersion;

    /// <summary>URL zur Dokumentation (für einen HyperlinkButton); <c>null</c> bei ungültigem Wert.</summary>
    public Uri? DocsUri =>
        Uri.TryCreate(_localization.GetText(SettingsTextKeys.DocsUrl), UriKind.Absolute, out Uri? uri) ? uri : null;

    /// <summary>
    /// Ziel des Spenden-Links. Anders als <see cref="DocsUri"/> stammt die Adresse
    /// <b>nicht</b> aus den Sprachdateien, sondern aus einer Compile-Zeit-Konstante:
    /// Ein zur Laufzeit austauschbares Spendenziel würde fremdes Geld umleiten.
    /// </summary>
    public Uri DonationUri => new(SupportDonation.PayPalUrl);

    /// <summary>
    /// Steuert die Sichtbarkeit des Spenden-Eintrags. Ohne echten Handle bleibt er
    /// verborgen, damit nie ein toter Link auf der Seite steht.
    /// </summary>
    public bool IsSupportVisible => SupportDonation.IsConfigured;
}
