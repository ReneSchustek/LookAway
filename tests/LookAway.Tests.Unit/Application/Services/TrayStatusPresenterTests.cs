using LookAway.Application.Services;
using LookAway.Core.Enums;

namespace LookAway.Tests.Unit.Application.Services;

/// <summary>
/// Tests der UI-freien Tray-Darstellungslogik (<see cref="TrayStatusPresenter"/>):
/// Icon-Variante pro Zustand und Tooltip-Formatierung.
/// </summary>
public sealed class TrayStatusPresenterTests
{
    private readonly TrayStatusPresenter _presenter = new();

    [Theory]
    [InlineData(TimerState.Working, TrayIconVariant.Working)]
    [InlineData(TimerState.OnBreak, TrayIconVariant.OnBreak)]
    [InlineData(TimerState.Paused, TrayIconVariant.Paused)]
    [InlineData(TimerState.Idle, TrayIconVariant.Paused)]
    public void GetIconVariant_MapsStateToVariant(TimerState state, TrayIconVariant expected)
    {
        Assert.Equal(expected, _presenter.GetIconVariant(state));
    }

    [Theory]
    [InlineData(TimerState.Working)]
    [InlineData(TimerState.OnBreak)]
    [InlineData(TimerState.Paused)]
    [InlineData(TimerState.Idle)]
    public void GetIconVariant_DndActive_AlwaysDisabled(TimerState state)
    {
        Assert.Equal(TrayIconVariant.Disabled, _presenter.GetIconVariant(state, isDndActive: true));
    }

    [Fact]
    public void GetTooltip_Working_ShowsRemainingAndModel()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.Working,
            TimeSpan.FromSeconds((23 * 60) + 45),
            BreakModel.ModifiedPomodoro);

        Assert.Contains("Naechste Pause in 23:45", tooltip, StringComparison.Ordinal);
        Assert.Contains("ModifiedPomodoro", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTooltip_OnBreak_ShowsRemaining()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.OnBreak,
            TimeSpan.FromSeconds((4 * 60) + 30),
            BreakModel.ClassicPomodoro);

        Assert.Equal("Pause laeuft (04:30 verbleibend)", tooltip);
    }

    [Fact]
    public void GetTooltip_Paused_ShowsPausedText()
    {
        string tooltip = _presenter.GetTooltip(TimerState.Paused, TimeSpan.Zero, BreakModel.ClassicPomodoro);

        Assert.Equal("Timer pausiert", tooltip);
    }

    [Fact]
    public void GetTooltip_Idle_ShowsStoppedText()
    {
        string tooltip = _presenter.GetTooltip(TimerState.Idle, TimeSpan.Zero, BreakModel.ClassicPomodoro);

        Assert.Equal("Timer gestoppt", tooltip);
    }

    [Fact]
    public void GetTooltip_DndActive_ShowsDndText()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.Working,
            TimeSpan.FromMinutes(10),
            BreakModel.ClassicPomodoro,
            isDndActive: true);

        Assert.Equal("DND aktiv — Erinnerungen ausgesetzt", tooltip);
    }

    [Fact]
    public void GetTooltip_RemainingAboveOneHour_FormatsTotalMinutes()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.Working,
            TimeSpan.FromMinutes(90),
            BreakModel.Ultradian);

        Assert.Contains("90:00", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTooltip_NegativeRemaining_ClampsToZero()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.OnBreak,
            TimeSpan.FromSeconds(-5),
            BreakModel.ClassicPomodoro);

        Assert.Equal("Pause laeuft (00:00 verbleibend)", tooltip);
    }
}
