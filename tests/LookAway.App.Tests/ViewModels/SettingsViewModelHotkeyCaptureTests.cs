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
    private static readonly bool[] BeginnUndEnde = [true, false];
    private static readonly bool[] NurBeginn = [true];
    private static readonly bool[] ZweiDurchgaenge = [true, false, true, false];

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
            statistics,
            new BreakModelListViewModel(localization, history),
            new LogViewModel(new FakeLogEntryReader(), localization, clock),
            new WorkTaskListViewModel(new FakeWorkTaskRepository(), history, localization, clock),
            NullLogger<SettingsViewModel>.Instance,
            "1.0.0");
    }

    [Fact]
    public void Aufnahme_ohne_Start_wird_abgelehnt()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);

        bool übernommen = viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkX));

        Assert.False(übernommen);
        Assert.False(viewModel.IsCapturingHotkey);
    }

    [Fact]
    public void Gültige_Kombination_wird_übernommen_und_beendet_die_Aufnahme()
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
    public void Kombination_ohne_echten_Modifikator_wird_abgelehnt()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        string vorher = viewModel.HotkeyStartBreakText;
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        bool übernommen = viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Shift, VkX));

        Assert.False(übernommen);
        Assert.Equal(vorher, viewModel.HotkeyStartBreakText);
        // Die Aufnahme läuft weiter, damit direkt eine gültige Kombination folgen kann.
        Assert.True(viewModel.IsCapturingHotkey);
    }

    [Fact]
    public void Bereits_belegte_Kombination_wird_abgelehnt()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        Assert.True(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkQ)));
        string andereBelegung = viewModel.HotkeySkipOrSnoozeText;

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.SkipOrSnooze);
        bool übernommen = viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkQ));

        Assert.False(übernommen);
        Assert.Equal(andereBelegung, viewModel.HotkeySkipOrSnoozeText);
    }

    [Fact]
    public void Dieselbe_Kombination_erneut_auf_dieselbe_Aktion_ist_keine_Kollision()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        HotkeyDefinition kombination = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkY);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);
        Assert.True(viewModel.TryCompleteHotkeyCapture(kombination));

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);
        bool erneut = viewModel.TryCompleteHotkeyCapture(kombination);

        Assert.True(erneut);
    }

    [Fact]
    public void Standardbelegung_einer_anderen_Aktion_wird_abgelehnt()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        string vorher = viewModel.HotkeyToggleDndText;
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);

        // Die Werksbelegung von "Pause starten" ist bereits vergeben — auch ohne
        // dass der Benutzer sie je selbst gesetzt hat.
        bool übernommen = viewModel.TryCompleteHotkeyCapture(HotkeyDefaults.StartBreak);

        Assert.False(übernommen);
        Assert.Equal(vorher, viewModel.HotkeyToggleDndText);
    }

    [Fact]
    public void Abbruch_lässt_die_Belegung_unverändert()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        string vorher = viewModel.HotkeyStartBreakText;
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        viewModel.CancelHotkeyCaptureCommand.Execute(null);

        Assert.False(viewModel.IsCapturingHotkey);
        Assert.Equal(vorher, viewModel.HotkeyStartBreakText);
        Assert.False(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control, VkX)));
        Assert.Equal(vorher, viewModel.HotkeyStartBreakText);
    }

    [Fact]
    public void Zweiter_Start_verschiebt_die_Aufnahme_auf_die_neue_Aktion()
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
    public async Task Aufgenommene_Belegung_wird_gespeichert()
    {
        using SettingsViewModel viewModel = CreateViewModel(out InMemorySettingsRepository repository);
        await viewModel.LoadAsync();
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.SkipOrSnooze);
        HotkeyDefinition kombination = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkQ);
        Assert.True(viewModel.TryCompleteHotkeyCapture(kombination));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Settings gespeichert = await repository.LoadAsync();
        Assert.Equal(kombination, gespeichert.HotkeySkipOrSnooze);
    }

    [Fact]
    public void Beginn_und_Übernahme_melden_den_Aufnahmezustand()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> meldungen = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => meldungen.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        Assert.True(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkX)));

        Assert.Equal(BeginnUndEnde, meldungen);
    }

    [Fact]
    public void Abgelehnte_Kombination_hält_den_Aufnahmezustand()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> meldungen = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => meldungen.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        // Ohne echten Modifikator: abgelehnt, aber die Aufnahme läuft weiter —
        // die Hotkeys dürfen deshalb noch nicht zurückkommen.
        Assert.False(viewModel.TryCompleteHotkeyCapture(new HotkeyDefinition(HotkeyModifiers.Shift, VkX)));

        Assert.Equal(NurBeginn, meldungen);
    }

    [Fact]
    public void Zweiter_Start_meldet_den_Zustand_nicht_erneut()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> meldungen = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => meldungen.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);

        Assert.Equal(NurBeginn, meldungen);
    }

    [Fact]
    public void Abbruch_und_Zurücksetzen_melden_das_Ende()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> meldungen = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => meldungen.Add(aktiv);

        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);
        viewModel.CancelHotkeyCaptureCommand.Execute(null);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.ToggleDnd);
        viewModel.ResetHotkeysCommand.Execute(null);

        Assert.Equal(ZweiDurchgaenge, meldungen);
    }

    [Fact]
    public void Freigeben_beendet_eine_offene_Aufnahme()
    {
        SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> meldungen = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => meldungen.Add(aktiv);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        // Fenster zu, während die Aufnahme läuft: Ohne Abmeldung blieben die
        // globalen Hotkeys dauerhaft freigegeben.
        viewModel.Dispose();

        Assert.Equal(BeginnUndEnde, meldungen);
    }

    [Fact]
    public void Freigeben_ohne_Aufnahme_meldet_nichts()
    {
        SettingsViewModel viewModel = CreateViewModel(out _);
        List<bool> meldungen = new();
        viewModel.HotkeyCaptureChanged += (_, aktiv) => meldungen.Add(aktiv);

        viewModel.Dispose();

        Assert.Empty(meldungen);
    }

    [Fact]
    public void Zurücksetzen_beendet_eine_laufende_Aufnahme()
    {
        using SettingsViewModel viewModel = CreateViewModel(out _);
        viewModel.BeginHotkeyCaptureCommand.Execute(HotkeyAction.StartBreak);

        viewModel.ResetHotkeysCommand.Execute(null);

        Assert.False(viewModel.IsCapturingHotkey);
        Assert.False(viewModel.TryCompleteHotkeyCapture(
            new HotkeyDefinition(HotkeyModifiers.Control, VkX)));
    }
}
