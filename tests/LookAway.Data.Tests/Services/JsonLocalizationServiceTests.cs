using System.Reflection;
using System.Text.Json;
using LookAway.Core.Enums;
using LookAway.Data.Services;

namespace LookAway.Data.Tests;

/// <summary>
/// Tests für den <see cref="JsonLocalizationService"/> gegen die echten,
/// eingebetteten Sprachtabellen.
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
