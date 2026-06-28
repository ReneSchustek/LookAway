using LookAway.Core.Enums;
using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// Test-Fake fuer <see cref="ILocalizationService"/>. Liefert sprachpraefigierte
/// Texte (<c>"&lt;Sprache&gt;:&lt;Schluessel&gt;"</c>), damit Sprachwechsel in Tests
/// sichtbar werden, und zaehlt die Sprachwechsel.
/// </summary>
internal sealed class FakeLocalizationService : ILocalizationService
{
    private Language _currentLanguage;

    /// <summary>Erzeugt den Fake mit einer Startsprache.</summary>
    /// <param name="initialLanguage">Anfangssprache.</param>
    public FakeLocalizationService(Language initialLanguage = Language.German)
    {
        _currentLanguage = initialLanguage;
    }

    /// <inheritdoc />
    public Language CurrentLanguage => _currentLanguage;

    /// <summary>Anzahl der <see cref="SetLanguage"/>-Aufrufe.</summary>
    public int SetLanguageCallCount { get; private set; }

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

    /// <inheritdoc />
    public string GetText(string key) => $"{_currentLanguage}:{key}";

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
