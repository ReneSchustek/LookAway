using System.Reflection;
using System.Text.RegularExpressions;
using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Enums;

namespace LookAway.App.Tests.ViewModels;

/// <summary>
/// Tests für <see cref="SettingsTexts"/>: die Beschriftungen des Einstellungsfensters.
/// </summary>
public sealed class SettingsTextsTests
{
    /// <summary>
    /// Findet <c>Texts.Irgendwas</c> in den Bindungsausdrücken der Ansichten.
    /// </summary>
    private static readonly Regex TextsBinding = new(
        @"\bTexts\.(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <remarks>
    /// Eine Beschriftung, die nichts liefert, hinterlässt eine leere Stelle im Fenster —
    /// und zwar erst zur Laufzeit und nur in der Sprache, in der der Schlüssel fehlt.
    /// </remarks>
    [Fact]
    public void EveryLabelReturnsText()
    {
        SettingsTexts texts = new(new FakeLocalizationService());
        List<string> empty = [];

        foreach (PropertyInfo property in TextProperties())
        {
            if (property.GetValue(texts) is not string value || string.IsNullOrWhiteSpace(value))
            {
                empty.Add(property.Name);
            }
        }

        Assert.True(
            empty.Count == 0,
            "Diese Beschriftungen liefern keinen Text: " + string.Join(", ", empty));
    }

    /// <remarks>
    /// Der eigentliche Zweck der Klasse: Nach dem Sprachwechsel meldet sie eine Änderung
    /// für alle Texte auf einmal, damit das Fenster nicht in gemischten Sprachen dasteht.
    /// </remarks>
    [Fact]
    public void RefreshDeliversTheNewLanguage()
    {
        FakeLocalizationService localization = new();
        SettingsTexts texts = new(localization);
        string before = texts.Title;

        localization.SetLanguage(Language.English);
        texts.Refresh();

        Assert.NotEqual(before, texts.Title);
        Assert.Contains("English", texts.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshAnnouncesAChangeToTheView()
    {
        SettingsTexts texts = new(new FakeLocalizationService());
        List<string?> announced = [];
        texts.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        texts.Refresh();

        // Ein leerer Name steht für "alles neu lesen" — genau das ist hier gemeint.
        Assert.Contains(announced, name => string.IsNullOrEmpty(name));
    }

    /// <remarks>
    /// Die Ansichten binden auf <c>Texts.Irgendwas</c>. Verschwindet oder verrutscht eine
    /// Eigenschaft, fällt das sonst erst auf, wenn jemand den Reiter öffnet — die Bindung
    /// scheitert still und die Stelle bleibt leer. Dieser Abgleich macht daraus einen
    /// roten Build.
    /// </remarks>
    [Fact]
    public void EveryBindingInTheViewsHasAProperty()
    {
        HashSet<string> available = [.. TextProperties().Select(property => property.Name)];
        List<string> missing = [];

        foreach (string file in ViewFiles())
        {
            foreach (Match match in TextsBinding.Matches(File.ReadAllText(file)))
            {
                string name = match.Groups["name"].Value;
                if (!available.Contains(name))
                {
                    missing.Add($"{Path.GetFileName(file)}: Texts.{name}");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "Diese Bindungen finden keine Eigenschaft in SettingsTexts:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing.Distinct()));
    }

    /// <remarks>
    /// Ohne diese Gegenprobe liefe die Prüfung oben ins Leere, sobald die Ansichten
    /// woanders liegen oder anders binden.
    /// </remarks>
    [Fact]
    public void TheViewsActuallyBindToTexts()
    {
        int found = ViewFiles().Sum(file => TextsBinding.Count(File.ReadAllText(file)));

        Assert.True(found > 20, $"Nur {found} Bindungen auf Texts gefunden.");
    }

    private static IEnumerable<PropertyInfo> TextProperties()
        => typeof(SettingsTexts)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string));

    private static IReadOnlyList<string> ViewFiles()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LookAway.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return
        [
            .. Directory.EnumerateFiles(
                Path.Combine(directory.FullName, "src", "LookAway.App", "Views"),
                "*.xaml",
                SearchOption.AllDirectories)
        ];
    }
}
