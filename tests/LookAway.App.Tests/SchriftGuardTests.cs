using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LookAway.App.Tests;

/// <summary>
/// Die Schriften reisen mit dem Programm: Jede Schrift, die die Oberfläche nennt, liegt als Datei
/// bei, wird ausgeliefert und hat ihren Lizenztext; aus dem Netz kommt keine.
/// </summary>
/// <remarks>
/// Fehlt eine Datei im Paket, fällt die Oberfläche still auf die Ersatzschrift zurück — beim
/// Entwickeln sieht das niemand, weil die Schrift dort vielleicht installiert ist.
/// </remarks>
public sealed class SchriftGuardTests
{
    private static readonly Regex FontReference = new(
        "<FontFamily x:Key=\"(?<key>[^\"]+)\">(?<value>[^<]+)</FontFamily>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void EveryFontInTheTokens_IsBundledAndShipped()
    {
        string app = AppProjectDirectory();
        HashSet<string> shipped = ContentItems(app);
        List<string> findings = [];

        foreach ((string key, string file) in FontFiles(app))
        {
            if (!File.Exists(Path.Combine(app, "Assets", "Fonts", file)))
            {
                findings.Add($"{key}: Datei Assets/Fonts/{file} fehlt");
            }

            if (!shipped.Contains($"Assets\\Fonts\\{file}"))
            {
                findings.Add($"{key}: Assets/Fonts/{file} ist nicht als Content ausgeliefert");
            }
        }

        Assert.True(findings.Count == 0, string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public void TheTokensUseRobotoFlexAndRobotoMono()
    {
        Dictionary<string, string> fonts = FontFiles(AppProjectDirectory()).ToDictionary(font => font.Key, font => font.File);

        Assert.Equal("RobotoFlex.ttf", fonts["RcFontFamily"]);
        Assert.Equal("RobotoFlex.ttf", fonts["ContentControlThemeFontFamily"]);
        Assert.Equal("RobotoMono.ttf", fonts["RcMonoFontFamily"]);
    }

    /// <remarks>Die SIL Open Font License verlangt, ihren Text mit der Schrift weiterzugeben.</remarks>
    [Fact]
    public void EveryBundledFont_HasItsLicenceAndNothingElseLiesThere()
    {
        string fonts = Path.Combine(AppProjectDirectory(), "Assets", "Fonts");
        string[] files = Directory.GetFiles(fonts).Select(Path.GetFileName).OfType<string>().Order().ToArray();

        Assert.Equal(["OFL-roboto-flex.txt", "OFL-roboto-mono.txt", "RobotoFlex.ttf", "RobotoMono.ttf"], files);
    }

    [Fact]
    public void NoFontComesFromTheNetwork()
    {
        string tokens = File.ReadAllText(Path.Combine(AppProjectDirectory(), "Themes", "Tokens.xaml"));

        foreach (Match match in FontReference.Matches(tokens))
        {
            Assert.DoesNotContain("http", match.Groups["value"].Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Jede FontFamily der Tokens, die eine eingebettete Datei nennt: Schlüssel und Dateiname.
    private static IEnumerable<(string Key, string File)> FontFiles(string app)
    {
        string tokens = File.ReadAllText(Path.Combine(app, "Themes", "Tokens.xaml"));

        foreach (Match match in FontReference.Matches(tokens))
        {
            string value = match.Groups["value"].Value;
            const string prefix = "ms-appx:///Assets/Fonts/";
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return (match.Groups["key"].Value, value[prefix.Length..value.IndexOf('#', StringComparison.Ordinal)]);
            }
        }
    }

    private static HashSet<string> ContentItems(string app)
        => XDocument.Load(Path.Combine(app, "LookAway.App.csproj"))
            .Descendants("Content")
            .Select(item => (string?)item.Attribute("Include"))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string AppProjectDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LookAway.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "src", "LookAway.App");
    }
}
