using LookAway.Core.Enums;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Liefert sprachabhaengige Anzeigetexte ueber sprachneutrale Schluessel und
/// erlaubt den Sprachwechsel zur Laufzeit.
/// </summary>
/// <remarks>
/// Die UI bindet an die uebersetzten Texte und aktualisiert sie bei
/// <see cref="LanguageChanged"/>. Aktuell ist nur Deutsch befuellt; Englisch und
/// Franzoesisch folgen und fallen bis dahin auf Deutsch zurueck.
/// </remarks>
public interface ILocalizationService
{
    /// <summary>Aktuell aktive Anzeigesprache.</summary>
    Language CurrentLanguage { get; }

    /// <summary>Wird ausgeloest, nachdem sich <see cref="CurrentLanguage"/> geaendert hat.</summary>
    event EventHandler? LanguageChanged;

    /// <summary>
    /// Liefert den uebersetzten Text fuer den Schluessel in der aktuellen Sprache.
    /// </summary>
    /// <param name="key">Sprachneutraler Schluessel (z. B. <c>"Settings.Title"</c>).</param>
    /// <returns>
    /// Der uebersetzte Text; faellt auf Deutsch zurueck, wenn die aktuelle
    /// Sprache den Schluessel nicht kennt, und auf den Schluessel selbst, wenn
    /// er in keiner Sprache existiert.
    /// </returns>
    string GetText(string key);

    /// <summary>
    /// Setzt die aktive Sprache. Bei einer tatsaechlichen Aenderung wird
    /// <see cref="LanguageChanged"/> ausgeloest.
    /// </summary>
    /// <param name="language">Neue Anzeigesprache.</param>
    void SetLanguage(Language language);
}
