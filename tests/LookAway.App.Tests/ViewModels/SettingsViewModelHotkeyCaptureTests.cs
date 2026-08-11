using LookAway.App.Tests.Fakes;
using LookAway.App.ViewModels;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.App.Tests;

/// <summary>
/// Tests für die Aufnahme einer eigenen Tastenkombination im
/// <see cref="SettingsViewModel"/>. Geprüft wird die Entscheidung — übernehmen,
/// ablehnen, abbrechen — nicht die Tastatur-Interaktion des Fensters.
/// </summary>
public sealed class SettingsViewModelHotkeyCaptureTests
{
    // Bewusst Tasten, die keine Standardbelegung tragen: Strg+Alt+P/S/D sind ab
    // Werk vergeben und würden in den Kollisionstests das Ergebnis verfälschen.
    private const int VkQ = 0x51;
    private const int VkX = 0x58;
    private const int VkY = 0x59;

    // Erwartete Folgen des Aufnahmezustands (true = Aufnahme läuft).
    private static readonly bool[] BeginAndEnd = [true, false];
    private static readonly bool[] BeginOnly = [true];
    private static readonly bool[] TwoRounds = [true, false, true, false];

    private static SettingsViewModel CreateViewModel(out InMemorySettingsRepository repository)
    {
        repository = new InMemorySettingsRepository(null);
        FakeLocalizationService localization = new();
        AutoStartCoordinator coordinator = new(
            new FakeAutoStartService(),
            repository,
            NullLogger<AutoStartCoordinator>.Instance);
        FakeBreakHistoryRepository history = new();
        FakeClock clock = new(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));
        StatisticsViewModel statistics = new(
            new StatisticsService(history, clock),
            history,
            new CsvExporter(),
            localization);

        return new SettingsViewModel(
            repository,
            coordinator,
            localization,
            new FakeSoundService(),
            new FakeUpdateChecker(),
            new FakeUpdateInstaller(),
            new SettingsSections(
                statistics,
                new BreakModelListViewModel(localization, history),
                new LogViewModel(new FakeLogEntryReader(), localization, clock),
                new WorkTaskListViewModel(new FakeWorkTaskRepository(), history, localization, clock)),
            NullLogger<SettingsViewModel>.Instance,
            "1.0.0");
    }

    [Fact]
    public void Capture_WithoutStart_IsRejected()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);

        bool übernommen = viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkX));

        Assert.False(übernommen);
        Assert.False(viewModel.IsCapturingHotkey);
    }

    [Fact]
    public void ValidCombination_IsAppliedAndEndsTheCapture()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        Assert.True(viewModel.IsCapturingHotkey);

        bool übernommen = viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkX));

        Assert.True(übernommen);
        Assert.False(viewModel.IsCapturingHotkey);
        Assert.Contains("X", viewModel.HotkeyStartBreakText, StringComparison.Ordinal);
    }

    [Fact]
    public void Combination_WithoutARealModifier_IsRejected()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        string before = viewModel.HotkeyStartBreakText;
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        bool übernommen = viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Shift, VkX));

        Assert.False(übernommen);
        Assert.Equal(before, viewModel.HotkeyStartBreakText);
        // Die Aufnahme läuft weiter, damit direkt eine gültige Kombination folgen kann.
        Assert.True(viewModel.IsCapturingHotkey);
    }

    [Fact]
    public void AlreadyAssignedCombination_IsRejected()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        Assert.True(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkQ)));
        string otherAssignment = viewModel.HotkeySkipOrSnoozeText;

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.SkipOrSnooze);
        bool übernommen = viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkQ));

        Assert.False(übernommen);
        Assert.Equal(otherAssignment, viewModel.HotkeySkipOrSnoozeText);
    }

    [Fact]
    public void SameCombinationOnTheSameAction_IsNoConflict()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        HotkeyDefinition combination = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkY);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);
        Assert.True(viewModel.TryCompleteHotkeyCapture(combination));

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);
        bool again = viewModel.TryCompleteHotkeyCapture(combination);

        Assert.True(again);
    }

    [Fact]
    public void DefaultAssignmentOfAnotherAction_IsRejected()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        string before = viewModel.HotkeyToggleDndText;
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);

        // Die Werksbelegung von "Pause starten" ist bereits vergeben — auch ohne
        // dass der Benutzer sie je selbst gesetzt hat.
        bool übernommen = viewModel.TryCompleteHotkeyCapture(HotkeyDefaults.StartBreak);

        Assert.False(übernommen);
        Assert.Equal(before, viewModel.HotkeyToggleDndText);
    }

    [Fact]
    public void Cancel_LeavesTheAssignmentUnchanged()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        string before = viewModel.HotkeyStartBreakText;
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        viewModel.CancelHotkeyCaptureCommand.Execute(null);

        Assert.False(viewModel.IsCapturingHotkey);
        Assert.Equal(before, viewModel.HotkeyStartBreakText);
        Assert.False(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control, VkX)));
        Assert.Equal(before, viewModel.HotkeyStartBreakText);
    }

    [Fact]
    public void SecondStart_MovesTheCaptureToTheNewAction()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        string startBreakVorher = viewModel.HotkeyStartBreakText;
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);

        Assert.True(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkX)));

        Assert.Equal(startBreakVorher, viewModel.HotkeyStartBreakText);
        Assert.Contains("X", viewModel.HotkeyToggleDndText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CapturedAssignment_IsSaved()
    {
        using SettingsViewModel viewModel = CreateViewModel(out InMemorySettingsRepository repository);
        await viewModel.LoadAsync();
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.SkipOrSnooze);
        HotkeyDefinition combination = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkQ);
        Assert.True(viewModel.TryCompleteHotkeyCapture(combination));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Settings saved = await repository.LoadAsync();
        Assert.Equal(combination, saved.HotkeySkipOrSnooze);
    }

    [Fact]
    public void StartAndCommit_ReportTheCaptureState()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> reports = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => reports.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        Assert.True(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkX)));

        Assert.Equal(BeginAndEnd, reports);
    }

    [Fact]
    public void RejectedCombination_KeepsTheCaptureState()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> reports = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => reports.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        // Ohne echten Modifikator: abgelehnt, aber die Aufnahme läuft weiter —
        // die Hotkeys dürfen deshalb noch nicht zurückkommen.
        Assert.False(viewModel.TryCompleteHotkeyCapture(new HotkeyDefinition(HotkeyModifiers.Shift, VkX)));

        Assert.Equal(BeginOnly, reports);
    }

    [Fact]
    public void SecondStart_DoesNotReportTheStateAgain()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> reports = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => reports.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);

        Assert.Equal(BeginOnly, reports);
    }

    [Fact]
    public void CancelAndReset_ReportTheEnd()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> reports = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => reports.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        viewModel.CancelHotkeyCaptureCommand.Execute(null);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);
        viewModel.ResetHotkeysCommand.Execute(null);

        Assert.Equal(TwoRounds, reports);
    }

    [Fact]
    public void Dispose_EndsAnOpenCapture()
    {
        SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> reports = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => reports.Add(aktiv);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        // Fenster zu, während die Aufnahme läuft: Ohne Abmeldung blieben die
        // globalen Hotkeys dauerhaft freigegeben.
        viewModel.Dispose();

        Assert.Equal(BeginAndEnd, reports);
    }

    [Fact]
    public void Dispose_WithoutCapture_ReportsNothing()
    {
        SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> reports = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => reports.Add(aktiv);

        viewModel.Dispose();

        Assert.Empty(reports);
    }

    [Fact]
    public void Reset_EndsARunningCapture()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        viewModel.ResetHotkeysCommand.Execute(null);

        Assert.False(viewModel.IsCapturingHotkey);
        Assert.False(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control, VkX)));
    }
}
