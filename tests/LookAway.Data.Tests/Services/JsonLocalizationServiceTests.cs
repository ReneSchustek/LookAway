using System.Reflection;
using System.Text.Json;
using LookAway.Core.Enums;
using LookAway.Core.Localization;
using LookAway.Data.Services;

namespace LookAway.Data.Tests;

/// <summary>
/// Die echten, eingebetteten Sprachtabellen: Texte je Sprache, Rückfall auf den Schlüssel
/// und dieselben Schlüssel in allen Sprachen. Fehlt einer in einer Sprache, stünde dort
/// unbemerkt der deutsche Text.
/// </summary>
public sealed class JsonLocalizationServiceTests
{
    [Fact]
    public void GetText_ReturnsTheGermanTextForAKnownKey()
    {
        JsonLocalizationService service = new(Language.German);

        string text = service.GetText("Settings.Title");

        Assert.Equal("Einstellungen", text);
    }

    [Fact]
    public void GetText_ReturnsTheEnglishText()
    {
        JsonLocalizationService service = new(Language.English);

        string text = service.GetText("Settings.Title");

        Assert.Equal("Settings", text);
    }

    [Fact]
    public void GetText_ReturnsTheFrenchText()
    {
        JsonLocalizationService service = new(Language.French);

        string text = service.GetText("Settings.Title");

        Assert.Equal("Paramètres", text);
    }

    [Fact]
    public void GetText_ReturnsTheKeyWhenItExistsNowhere()
    {
        JsonLocalizationService service = new(Language.German);

        string text = service.GetText("Nicht.Vorhanden.Schlüssel");

        Assert.Equal("Nicht.Vorhanden.Schlüssel", text);
    }

    [Fact]
    public void SetLanguage_RaisesLanguageChangedOnlyOnARealChange()
    {
        JsonLocalizationService service = new(Language.German);
        int raised = 0;
        service.LanguageChanged += (_, _) => raised++;

        service.SetLanguage(Language.German); // keine Änderung
        service.SetLanguage(Language.French); // Änderung

        Assert.Equal(1, raised);
        Assert.Equal(Language.French, service.CurrentLanguage);
    }

    [Theory]
    [InlineData("en.json")]
    [InlineData("fr.json")]
    public void AllGermanKeys_ExistInTheOtherLanguages(string otherFile)
    {
        HashSet<string> german = LoadKeys("de.json");
        HashSet<string> other = LoadKeys(otherFile);

        string[] missing = german.Except(other).Order().ToArray();
        Assert.True(missing.Length == 0, $"Fehlende Schlüssel in {otherFile}: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("en.json")]
    [InlineData("fr.json")]
    public void NoSurplusKeys_InTheOtherLanguages(string otherFile)
    {
        HashSet<string> german = LoadKeys("de.json");
        HashSet<string> other = LoadKeys(otherFile);

        string[] extra = other.Except(german).Order().ToArray();
        Assert.True(extra.Length == 0, $"Überzählige Schlüssel in {otherFile}: {string.Join(", ", extra)}");
    }

    /// <remarks>
    /// Ein Schlüssel, den der Code nennt, aber keine Datei kennt, fiele erst auf der Oberfläche auf:
    /// Dort stünde dann der Schlüssel selbst statt des Texts.
    /// </remarks>
    [Fact]
    public void EveryDeclaredKey_ExistsInGerman()
    {
        HashSet<string> german = LoadKeys("de.json");
        IEnumerable<string> declared = typeof(SettingsTextKeys).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(SettingsTextKeys).Namespace && type.Name.EndsWith("TextKeys", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!); // Konstanten vom Typ string sind nie null

        string[] missing = declared.Where(key => !german.Contains(key)).Order().ToArray();
        Assert.True(missing.Length == 0, $"Fehlende Schlüssel in de.json: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData(OverlayBackground.Color)]
    [InlineData(OverlayBackground.Image)]
    public void EveryOverlayBackground_HasAGermanLabel(OverlayBackground background)
        => Assert.Contains(SettingsTextKeys.ForOverlayBackground(background), LoadKeys("de.json"));

    private static HashSet<string> LoadKeys(string fileName)
    {
        string resourceName = "LookAway.Data.Localization." + fileName;
        Assembly assembly = typeof(JsonLocalizationService).Assembly;

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Ressource {resourceName} fehlt.");
        Dictionary<string, string>? entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return entries is null ? new HashSet<string>() : new HashSet<string>(entries.Keys);
    }
}
