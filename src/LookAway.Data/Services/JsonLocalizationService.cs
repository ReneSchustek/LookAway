using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Lokalisierung ueber eingebettete JSON-Ressourcen (eine Datei je Sprache).
/// Deutsch ist die Referenzsprache und dient als Fallback; Englisch und
/// Franzoesisch werden befuellt und fallen bis dahin auf Deutsch
/// zurueck.
/// </summary>
public sealed class JsonLocalizationService : ILocalizationService
{
    private const string ResourcePrefix = "LookAway.Data.Localization.";

    private readonly FrozenDictionary<Language, FrozenDictionary<string, string>> _tables;
    private Language _currentLanguage;

    /// <summary>
    /// Laedt die eingebetteten Sprachtabellen aus dem Data-Assembly.
    /// </summary>
    /// <param name="initialLanguage">Beim Start aktive Sprache.</param>
    public JsonLocalizationService(Language initialLanguage = Language.German)
    {
        _tables = LoadTables();
        _currentLanguage = initialLanguage;
    }

    /// <inheritdoc />
    public Language CurrentLanguage => _currentLanguage;

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

    /// <inheritdoc />
    public string GetText(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_tables.TryGetValue(_currentLanguage, out FrozenDictionary<string, string>? table)
            && table.TryGetValue(key, out string? value))
        {
            return value;
        }

        // Fallback auf Deutsch (Referenzsprache), sonst der Schluessel selbst.
        if (_currentLanguage != Language.German
            && _tables.TryGetValue(Language.German, out FrozenDictionary<string, string>? german)
            && german.TryGetValue(key, out string? germanValue))
        {
            return germanValue;
        }

        return key;
    }

    /// <inheritdoc />
    public void SetLanguage(Language language)
    {
        if (!Enum.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unbekannte Sprache.");
        }

        if (_currentLanguage == language)
        {
            return;
        }

        _currentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static FrozenDictionary<Language, FrozenDictionary<string, string>> LoadTables()
    {
        Dictionary<Language, FrozenDictionary<string, string>> tables = new();
        foreach (Language language in Enum.GetValues<Language>())
        {
            tables[language] = LoadTable(language);
        }

        return tables.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, string> LoadTable(Language language)
    {
        string resourceName = ResourcePrefix + GetResourceFileName(language);
        Assembly assembly = typeof(JsonLocalizationService).Assembly;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return FrozenDictionary<string, string>.Empty;
        }

        Dictionary<string, string>? entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return entries is null
            ? FrozenDictionary<string, string>.Empty
            : entries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static string GetResourceFileName(Language language) => language switch
    {
        Language.German => "de.json",
        Language.English => "en.json",
        Language.French => "fr.json",
        _ => "de.json",
    };
}
