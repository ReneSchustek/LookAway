using LookAway.App.ViewModels;

namespace LookAway.App.Tests.ViewModels;

/// <summary>
/// Tests für <see cref="ListViewState"/>: die Unterscheidung, die jede Listen-Ansicht
/// treffen muss — nichts vorhanden oder nichts gefunden.
/// </summary>
public sealed class ListViewStateTests
{
    [Fact]
    public void WithEntries_HasResults()
    {
        ListViewState state = new(Total: 5, Visible: 5);

        Assert.True(state.HasResults);
        Assert.False(state.ShowEmpty);
        Assert.False(state.ShowNoResults);
    }

    /// <remarks>
    /// Hier hilft kein Zurücksetzen der Suche — es ist schlicht nichts da.
    /// </remarks>
    [Fact]
    public void WithoutAnyEntry_ShowsEmpty()
    {
        ListViewState state = new(Total: 0, Visible: 0);

        Assert.False(state.HasResults);
        Assert.True(state.ShowEmpty);
        Assert.False(state.ShowNoResults);
    }

    /// <remarks>
    /// Und hier ist etwas da, nur nicht das Gesuchte — dieser Fall bekommt die
    /// Schaltfläche zum Zurücksetzen.
    /// </remarks>
    [Fact]
    public void WithEntriesButNoMatch_ShowsNoResults()
    {
        ListViewState state = new(Total: 5, Visible: 0);

        Assert.False(state.HasResults);
        Assert.False(state.ShowEmpty);
        Assert.True(state.ShowNoResults);
    }

    [Fact]
    public void PartiallyFiltered_StillHasResults()
    {
        ListViewState state = new(Total: 5, Visible: 2);

        Assert.True(state.HasResults);
        Assert.False(state.ShowEmpty);
        Assert.False(state.ShowNoResults);
    }

    /// <remarks>
    /// Die beiden Leerzustände schließen einander aus — sonst stünden zwei
    /// widersprüchliche Sätze übereinander in der Ansicht.
    /// </remarks>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(3, 1)]
    [InlineData(9, 9)]
    public void EmptyAndNoResults_AreNeverBothTrue(int total, int visible)
    {
        ListViewState state = new(total, visible);

        Assert.False(state.ShowEmpty && state.ShowNoResults);
    }
}
