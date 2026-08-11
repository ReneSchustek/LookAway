using LookAway.Core.Services;
using LookAway.Core.Enums;
using LookAway.Core.Tests.Fakes;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests der UI-freien Tray-Darstellungslogik (<see cref="TrayStatusPresenter"/>):
/// Icon-Variante pro Zustand und Tooltip-Formatierung. Die Tooltip-Texte stammen
/// aus der (gefakten) Lokalisierung; geprüft werden Schlüsselwahl und Formatierung.
/// </summary>
public sealed class TrayStatusPresenterTests
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["Tray.Tooltip.Dnd"] = "DND aktiv",
        ["Tray.Tooltip.Paused"] = "Timer pausiert",
        ["Tray.Tooltip.Idle"] = "Timer gestoppt",
        ["Tray.Tooltip.NextBreak"] = "Nächste Pause in {0}",
        ["Tray.Tooltip.Model"] = "Modell: {0}",
        ["Tray.Tooltip.Task"] = "Aufgabe: {0}",
        ["Settings.Model.TaskBased"] = "TaskBased",
        ["Tray.Tooltip.OnBreak"] = "Pause läuft ({0} verbleibend)",
        ["Settings.Model.ModifiedPomodoro"] = "ModifiedPomodoro",
        ["Settings.Model.ClassicPomodoro"] = "ClassicPomodoro",
        ["Settings.Model.Ultradian"] = "Ultradian",
    };

    private readonly TrayStatusPresenter _presenter =
        new(new FakeLocalizationService(Language.German, Texts));

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

        Assert.Contains("Nächste Pause in 23:45", tooltip, StringComparison.Ordinal);
        Assert.Contains("ModifiedPomodoro", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTooltip_OnBreak_ShowsRemaining()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.OnBreak,
            TimeSpan.FromSeconds((4 * 60) + 30),
            BreakModel.ClassicPomodoro);

        Assert.Equal("Pause läuft (04:30 verbleibend)", tooltip);
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

        Assert.Equal("DND aktiv", tooltip);
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

        Assert.Equal("Pause läuft (00:00 verbleibend)", tooltip);
    }
    /// <remarks>
    /// Beim aufgabenbasierten Modell sagt die Restzeit wenig — dort zählt, woran
    /// gerade gearbeitet wird.
    /// </remarks>
    [Fact]
    public void GetTooltip_NamesTheCurrentTaskForTheTaskBasedModel()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.Working,
            TimeSpan.FromMinutes(30),
            BreakModel.TaskBased,
            isDndActive: false,
            currentTask: "Angebot schreiben");

        Assert.Contains("Aufgabe: Angebot schreiben", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTooltip_OmitsTheTaskForOtherModels()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.Working,
            TimeSpan.FromMinutes(30),
            BreakModel.ClassicPomodoro,
            isDndActive: false,
            currentTask: "Angebot schreiben");

        Assert.DoesNotContain("Aufgabe:", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTooltip_OmitsTheTaskLineWithoutATask()
    {
        string tooltip = _presenter.GetTooltip(
            TimerState.Working,
            TimeSpan.FromMinutes(30),
            BreakModel.TaskBased,
            isDndActive: false,
            currentTask: null);

        Assert.DoesNotContain("Aufgabe:", tooltip, StringComparison.Ordinal);
    }

}
