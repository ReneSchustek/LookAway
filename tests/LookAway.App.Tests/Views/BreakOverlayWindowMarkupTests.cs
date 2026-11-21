using System.Xml.Linq;

namespace LookAway.App.Tests.Views;

/// <summary>
/// Das Pausen-Overlay zeigt zu seinen Tastenkürzeln keinen Tooltip, und Esc beendet die Pause
/// weiterhin.
/// </summary>
/// <remarks>
/// Ohne <c>KeyboardAcceleratorPlacementMode="Hidden"</c> blendet WinUI die Taste ein, sobald der
/// Zeiger über dem Element ruht. Beim bildschirmfüllenden Overlay stünde dann „Esc“ an jeder
/// Stelle, an der die Maus anhält. Geprüft wird das Markup, weil das Fenster ohne Desktop nicht
/// aufgebaut werden kann.
/// </remarks>
public sealed class BreakOverlayWindowMarkupTests
{
    private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace Presentation = Xaml;

    [Fact]
    public void EveryElementWithAccelerators_HidesTheirTooltip()
    {
        XDocument overlay = XDocument.Load(OverlayMarkupPath());

        Assert.Empty(ElementsShowingAcceleratorTooltips(overlay));
    }

    [Fact]
    public void EscapeStillEndsTheBreak()
    {
        XDocument overlay = XDocument.Load(OverlayMarkupPath());

        XElement escape = Assert.Single(
            overlay.Descendants(Presentation + "KeyboardAccelerator"),
            accelerator => (string?)accelerator.Attribute("Key") == "Escape");
        Assert.Equal("OnEscapeInvoked", (string?)escape.Attribute("Invoked"));
    }

    // Gegenprobe: Ohne das Attribut muss die Prüfung das Element finden, sonst stünde sie
    // blind grün.
    [Theory]
    [InlineData("<Grid xmlns=\"" + Xaml + "\"><Grid.KeyboardAccelerators><KeyboardAccelerator Key=\"Escape\" /></Grid.KeyboardAccelerators></Grid>", 1)]
    [InlineData("<Grid xmlns=\"" + Xaml + "\" KeyboardAcceleratorPlacementMode=\"Auto\"><Grid.KeyboardAccelerators><KeyboardAccelerator Key=\"Escape\" /></Grid.KeyboardAccelerators></Grid>", 1)]
    [InlineData("<Grid xmlns=\"" + Xaml + "\" KeyboardAcceleratorPlacementMode=\"Hidden\"><Grid.KeyboardAccelerators><KeyboardAccelerator Key=\"Escape\" /></Grid.KeyboardAccelerators></Grid>", 0)]
    [InlineData("<Grid xmlns=\"" + Xaml + "\"><TextBlock /></Grid>", 0)]
    public void TheCheck_FindsMissingPlacementModes(string markup, int expected)
    {
        ArgumentNullException.ThrowIfNull(markup);
        Assert.Equal(expected, ElementsShowingAcceleratorTooltips(XDocument.Parse(markup)).Count);
    }

    // Ein Element trägt Tastenkürzel, wenn es ein Eigenschaftselement "<Typ>.KeyboardAccelerators"
    // enthält. Zählt nur das Element selbst, nicht seine Kinder: Das Attribut wirkt dort, wo die
    // Kürzel hängen.
    private static List<string> ElementsShowingAcceleratorTooltips(XDocument markup)
        => [.. markup.Descendants()
            .Where(element => element.Name.LocalName.EndsWith(".KeyboardAccelerators", StringComparison.Ordinal))
            .Select(property => property.Parent!)
            .Where(owner => (string?)owner.Attribute("KeyboardAcceleratorPlacementMode") != "Hidden")
            .Select(owner => owner.Name.LocalName)];

    private static string OverlayMarkupPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LookAway.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "LookAway.App", "Views", "BreakOverlayWindow.xaml");
    }
}
