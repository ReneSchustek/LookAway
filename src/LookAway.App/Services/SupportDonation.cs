using System;

namespace LookAway.App.Services;

/// <summary>
/// Fest verdrahtetes Spendenziel für den freiwilligen „Kaffee spendieren"-Eintrag
/// auf der Über-Seite.
/// <para>
/// Die Ziel-URL ist bewusst eine Compile-Zeit-Konstante und wird <b>niemals</b> aus
/// Settings, Datenbank, Lokalisierung oder Netz-Antwort gelesen — so gibt es zur
/// Laufzeit keinen austauschbaren Wert, über den jemand das Geld umleiten könnte.
/// Das ist der Unterschied zur Dokumentations-Adresse daneben, die aus den
/// Sprachdateien stammt: Dort ist ein falscher Link ärgerlich, hier wäre er teuer.
/// </para>
/// </summary>
internal static class SupportDonation
{
    /// <summary>
    /// Offizielle PayPal-Spenden-URL (HTTPS, ohne Laufzeit-Parameter).
    /// Fest verdrahtet auf den paypal.me-Handle des Entwicklers.
    /// </summary>
    public const string PayPalUrl = "https://paypal.me/rschustek";

    /// <summary>
    /// Platzhalter-Segment im noch nicht konfigurierten Link. Solange es in
    /// <see cref="PayPalUrl"/> steht, bleibt der Spenden-Eintrag ausgeblendet
    /// (<see cref="IsConfigured"/>), damit nie ein toter Link erscheint.
    /// </summary>
    private const string Placeholder = "PLATZHALTER";

    /// <summary>
    /// <see langword="true"/>, sobald ein echter Handle eingetragen ist (kein Platzhalter mehr).
    /// Steuert die Sichtbarkeit des Spenden-Eintrags.
    /// </summary>
    public static bool IsConfigured => !PayPalUrl.Contains(Placeholder, StringComparison.Ordinal);
}
