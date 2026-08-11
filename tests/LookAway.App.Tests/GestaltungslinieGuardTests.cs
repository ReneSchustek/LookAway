using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LookAway.App.Tests;

/// <summary>
/// Hält die projektübergreifende Gestaltungslinie ein.
/// </summary>
/// <remarks>
/// Eine Gestaltungslinie, die nur in einem Dokument steht, hält kein Jahr: Nach dem dritten
/// „nur hier einmal schnell" ist sie Papier. Diese Prüfungen machen den Build rot, statt auf
/// gutes Zureden zu hoffen.
/// </remarks>
public class GestaltungslinieGuardTests
{
    // Farbwerte gehören in die Belegungen, nicht in eine Ansicht. „Transparent" ist keine
    // Farbe der Marke, sondern eine Aussage über die Fläche — deshalb erlaubt.
    private static readonly Regex ColorAttribute = new(
        "(Background|Foreground|BorderBrush|Fill|Stroke|Color)\\s*=\\s*"
        + "\"(#[0-9A-Fa-f]{3,8}|White|Black|Gray|LightGray|DarkGray|Silver|Red|Green|Blue|Yellow|Orange|Navy|Teal)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <remarks>
    /// Wer in einer Ansicht eine Hex-Zahl schreibt, hat die Linie an dieser Stelle verlassen —
    /// und die Stelle bleibt beim Wechsel des Erscheinungsbilds stehen, während alles andere
    /// mitgeht.
    /// </remarks>
    [Fact]
    public void ViewsAndControlsContainNoColorValues()
    {
        List<string> findings = [];

        foreach (string file in ViewAndControlFiles())
        {
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                Match match = ColorAttribute.Match(lines[index]);
                if (match.Success)
                {
                    findings.Add($"{Path.GetFileName(file)}:{index + 1}  {match.Value.Trim()}");
                }
            }
        }

        Assert.True(
            findings.Count == 0,
            "Farbwerte gehören in Themes/Light.xaml und Themes/Dark.xaml, nicht in eine Ansicht:"
            + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    /// <remarks>
    /// Fehlt ein Schlüssel in einer Belegung, bricht die Bindung genau in dieser Belegung —
    /// und das fällt erst beim Nutzer auf, nicht beim Entwickeln.
    /// </remarks>
    [Fact]
    public void LightAndDarkDefineTheSameKeys()
    {
        HashSet<string> light = ResourceKeys("Light.xaml");
        HashSet<string> dark = ResourceKeys("Dark.xaml");

        List<string> onlyLight = [.. light.Except(dark).Order()];
        List<string> onlyDark = [.. dark.Except(light).Order()];

        Assert.True(
            onlyLight.Count == 0 && onlyDark.Count == 0,
            "Light.xaml und Dark.xaml müssen denselben Schlüsselsatz führen."
            + Environment.NewLine + "Nur in Light: " + string.Join(", ", onlyLight)
            + Environment.NewLine + "Nur in Dark: " + string.Join(", ", onlyDark));
    }

    /// <remarks>
    /// Tokens gelten in beiden Belegungen. Ein Farbwert an dieser Stelle wäre in einer der
    /// beiden immer falsch — und ließe sich nirgends korrigieren, ohne die andere zu treffen.
    /// </remarks>
    [Fact]
    public void TokensContainNoColorValues()
    {
        string path = Path.Combine(AppProjectDirectory(), "Themes", "Tokens.xaml");
        List<string> findings = [];
        string[] lines = File.ReadAllLines(path);

        for (int index = 0; index < lines.Length; index++)
        {
            if (ColorAttribute.IsMatch(lines[index]) || lines[index].Contains("SolidColorBrush", StringComparison.Ordinal))
            {
                findings.Add($"Tokens.xaml:{index + 1}  {lines[index].Trim()}");
            }
        }

        Assert.True(
            findings.Count == 0,
            "Tokens.xaml führt Abstände, Radien und Schrift — Farben gehören in die Belegungen:"
            + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    /// <remarks>
    /// Fängt den Fall ab, dass die Prüfungen oben mangels gefundener Dateien grün wären.
    /// </remarks>
    [Fact]
    public void PalettesAndViewsAreNotEmpty()
    {
        Assert.NotEmpty(ResourceKeys("Light.xaml"));
        Assert.NotEmpty(ResourceKeys("Dark.xaml"));
        Assert.NotEmpty(ViewAndControlFiles());
    }

    private static IReadOnlyList<string> ViewAndControlFiles()
    {
        string app = AppProjectDirectory();

        return
        [
            .. Directory.EnumerateFiles(Path.Combine(app, "Views"), "*.xaml", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(Path.Combine(app, "Controls"), "*.xaml", SearchOption.AllDirectories),
        ];
    }

    private static HashSet<string> ResourceKeys(string paletteFile)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        string path = Path.Combine(AppProjectDirectory(), "Themes", paletteFile);

        return
        [
            .. XDocument.Load(path)
                .Descendants()
                .Select(element => element.Attribute(x + "Key")?.Value)
                .Where(key => key is not null)
                .Select(key => key!)
        ];
    }

    // Vom Testausgabeverzeichnis nach oben, bis die Projektmappe auftaucht. So bleibt der
    // Pfad unabhängig davon, wo das Repository liegt.
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
