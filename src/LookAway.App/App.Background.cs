using LookAway.Core.Entities;
using LookAway.Core.Exceptions;
using LookAway.Core.Interfaces;
using LookAway.App.Services;
using Microsoft.Extensions.DependencyInjection;
using AutoStartCoordinator = LookAway.Core.Services.AutoStartCoordinator;
using CurrentWorkTaskTracker = LookAway.Core.Services.CurrentWorkTaskTracker;

namespace LookAway.App;

/// <summary>
/// Arbeit neben dem Start und während des Laufs: Countdown sichern, Hotkeys während einer Aufnahme
/// freigeben, Historie aufräumen, Autostart abgleichen, laufende Aufgabe bestimmen und geänderte
/// Einstellungen übernehmen.
/// </summary>
public sealed partial class LookAwayApp
{
    /// <summary>
    /// Sichert beim Beenden den laufenden Arbeits-Countdown samt Sitzungsmarke, damit
    /// ihn ein Neustart in derselben Sitzung fortsetzen kann. Nur während einer
    /// Arbeitsphase sinnvoll; in Pause/Idle wird nichts gesichert.
    /// </summary>
    private void PersistTimerSnapshot()
    {
        if (_coordinator is { IsWorking: true })
        {
            CreateTimerResumption().Remember(_coordinator.ActiveModel, _coordinator.WorkRemaining);
        }
    }

    private TimerResumption CreateTimerResumption() => new(
        Services.GetRequiredService<ITimerStateStore>(),
        Services.GetRequiredService<IClock>(),
        static () => Environment.TickCount64);

    /// <summary>
    /// Gibt die globalen Hotkeys frei, solange in den Einstellungen eine
    /// Tastenkombination aufgenommen wird, und registriert sie danach wieder.
    /// <para>
    /// Ohne das erreicht die gedrückte Kombination das Fenster nicht: Windows
    /// liefert einen registrierten Hotkey an den Registrierenden aus, nicht an das
    /// fokussierte Fenster. Der Benutzer löste damit die Aktion aus, statt sie neu
    /// zu belegen.
    /// </para>
    /// </summary>
    /// <param name="captureRunning">Wahr bei Beginn, falsch bei Ende der Aufnahme.</param>
    private void SuspendHotkeysWhileCapturing(bool captureRunning)
        => _hotkeys?.SetCaptureRunning(captureRunning);

    private async Task PurgeHistoryAsync()
    {
        try
        {
            DateTimeOffset cutoff = Services.GetRequiredService<IClock>().UtcNow.AddDays(-HistoryRetentionDays);
            _ = await Services.GetRequiredService<IBreakHistoryRepository>()
                .PurgeOlderThanAsync(cutoff)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.HistoryWriteFailed(_logger, ex);
        }
        catch (IOException ex)
        {
            AppLog.HistoryWriteFailed(_logger, ex);
        }
    }

    private async Task SynchronizeAutoStartAsync()
    {
        AutoStartCoordinator coordinator = Services.GetRequiredService<AutoStartCoordinator>();
        try
        {
            _ = await coordinator.SynchronizeFromRegistryAsync().ConfigureAwait(false);
        }
        catch (AutoStartException ex)
        {
            // Autostart ist optional — ein Fehler darf den Start nicht abbrechen.
            AppLog.AutoStartSyncFailed(_logger, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.AutoStartSyncFailed(_logger, ex);
        }
        catch (IOException ex)
        {
            AppLog.AutoStartSyncFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Bestimmt nach einer Änderung an den Aufgaben neu, an welcher gerade gearbeitet
    /// wird. Die Antwort braucht der Timer beim Aufzeichnen der Pause sofort, und der
    /// Infobereich nennt sie im Kurztext.
    /// </summary>
    private void RefreshCurrentWorkTask() => _ = RefreshCurrentWorkTaskAsync();

    private async Task RefreshCurrentWorkTaskAsync()
    {
        try
        {
            CurrentWorkTaskTracker tracker = Services.GetRequiredService<CurrentWorkTaskTracker>();
            await tracker.RefreshAsync().ConfigureAwait(false);
            _trayIcon?.SetCurrentTask(tracker.CurrentTaskText);
        }
        catch (IOException ex)
        {
            // Die laufende Aufgabe ist eine Bequemlichkeit, kein Startkriterium.
            AppLog.CurrentTaskRefreshFailed(_logger, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.CurrentTaskRefreshFailed(_logger, ex);
        }
    }

    /// <summary>
    /// Übernimmt gespeicherte Einstellungen sofort: startet den Timer mit dem
    /// neuen Modell neu und aktualisiert Idle-/Vollbild-Erkennung sowie das Tray.
    /// </summary>
    private void ApplySettingsLive(Settings settings)
    {
        _coordinator?.ApplySchedule(settings);
        _detectionLoop?.ApplySettings(settings);
        Services.GetRequiredService<ThemeService>().SetTheme(settings.AppTheme);
        _hotkeys?.Apply(settings);
    }
}
