using LookAway.App.Services;

namespace LookAway.App.Tests;

/// <summary>
/// Reihenfolge der Overlay-Fenster: Der Hauptmonitor kommt zuletzt, damit sein Fenster den Fokus
/// behält und Esc dort wirkt, wo der Inhalt steht.
/// </summary>
public sealed class DisplayOrderTests
{
    [Fact]
    public void AllScreens_PutsThePrimaryLast()
    {
        IReadOnlyList<ulong> order = DisplayOrder.Arrange(2UL, [1UL, 2UL, 3UL], static id => id, allScreens: true);

        Assert.Equal([1UL, 3UL, 2UL], order);
    }

    [Fact]
    public void PrimaryOnly_ShowsJustThePrimary()
    {
        IReadOnlyList<ulong> order = DisplayOrder.Arrange(2UL, [1UL, 2UL, 3UL], static id => id, allScreens: false);

        Assert.Equal([2UL], order);
    }

    [Fact]
    public void SingleScreen_ShowsItOnce()
    {
        IReadOnlyList<ulong> order = DisplayOrder.Arrange(7UL, [7UL], static id => id, allScreens: true);

        Assert.Equal([7UL], order);
    }
}
