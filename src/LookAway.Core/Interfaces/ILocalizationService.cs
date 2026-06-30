using LookAway.Core.Enums;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Liefert sprachabhängige Anzeigetexte über sprachneutrale Schlüssel und
/// erlaubt den Sprachwechsel zur Laufzeit.
/// </summary>
/// <remarks>
/// Die UI bindet an die übersetzten Texte und aktualisiert sie bei
/// <see cref="LanguageChanged"/>. Deutsch ist die Referenzsprache; fehlt ein
/// Schlüssel in einer anderen Sprache, wird auf Deutsch zurückgegriffen.
/// </remarks>
public interface ILocalizationService
{
    /// <summary>Aktuell aktive Anzeigesprache.</summary>
    Language CurrentLanguage { get; }

    /// <summary>Wird ausgelöst, nachdem sich <see cref="CurrentLanguage"/> geändert hat.</summary>
    event EventHandler? LanguageChanged;

    /// <summary>
    /// Liefert den übersetzten Text für den Schlüssel in der aktuellen Sprache.
    /// </summary>
    /// <param name="key">Sprachneutraler Schlüssel (z. B. <c>"Settings.Title"</c>).</param>
    /// <returns>
    /// Der übersetzte Text; fällt auf Deutsch zurück, wenn die aktuelle
    /// Sprache den Schlüssel nicht kennt, und auf den Schlüssel selbst, wenn
    /// er in keiner Sprache existiert.
    /// </returns>
    string GetText(string key);

    /// <summary>
    /// Setzt die aktive Sprache. Bei einer tatsächlichen Änderung wird
    /// <see cref="LanguageChanged"/> ausgelöst.
    /// </summary>
    /// <param name="language">Neue Anzeigesprache.</param>
    void SetLanguage(Language language);
}
