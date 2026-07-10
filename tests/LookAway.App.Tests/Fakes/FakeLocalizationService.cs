using LookAway.Core.Enums;
using LookAway.Core.Interfaces;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Test-Fake für <see cref="ILocalizationService"/>. Liefert sprachpräfigierte
/// Texte (<c>"&lt;Sprache&gt;:&lt;Schlüssel&gt;"</c>), damit Sprachwechsel in Tests
/// sichtbar werden, und zählt die Sprachwechsel.
/// </summary>
internal sealed class FakeLocalizationService : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, string>? _table;
    private Language _currentLanguage;

    /// <summary>Erzeugt den Fake mit einer Startsprache.</summary>
    /// <param name="initialLanguage">Anfangssprache.</param>
    /// <param name="table">
    /// Optionale Texttabelle (Schlüssel → Vorlage). Fehlt ein Schlüssel, wird
    /// <c>"&lt;Sprache&gt;:&lt;Schlüssel&gt;"</c> geliefert.
    /// </param>
    public FakeLocalizationService(
        Language initialLanguage = Language.German,
        IReadOnlyDictionary<string, string>? table = null)
    {
        _currentLanguage = initialLanguage;
        _table = table;
    }

    /// <inheritdoc />
    public Language CurrentLanguage => _currentLanguage;

    /// <summary>Anzahl der <see cref="SetLanguage"/>-Aufrufe.</summary>
    public int SetLanguageCallCount { get; private set; }

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

    /// <inheritdoc />
    public string GetText(string key)
        => _table is not null && _table.TryGetValue(key, out string? value)
            ? value
            : $"{_currentLanguage}:{key}";

    /// <inheritdoc />
    public void SetLanguage(Language language)
    {
        SetLanguageCallCount++;
        if (_currentLanguage == language)
        {
            return;
        }

        _currentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
