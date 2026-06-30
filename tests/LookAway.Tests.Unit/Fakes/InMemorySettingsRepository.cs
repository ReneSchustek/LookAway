using LookAway.Core.Entities;
using LookAway.Core.Interfaces;

namespace LookAway.Tests.Unit.Fakes;

/// <summary>
/// In-Memory-Fake für <see cref="ISettingsRepository"/>. Liefert bei jedem
/// <see cref="LoadAsync"/> eine Kopie des gehaltenen Zustands (wie ein echtes
/// Repository über die Datei-Deserialisierung) und zählt Speichervorgänge.
/// </summary>
internal sealed class InMemorySettingsRepository : ISettingsRepository
{
    private Settings _settings;

    /// <summary>Erzeugt das Fake-Repository mit einem Anfangszustand.</summary>
    /// <param name="initial">Startwerte; <c>null</c> erzeugt First-Run-Defaults.</param>
    public InMemorySettingsRepository(Settings? initial = null)
    {
        _settings = Clone(initial ?? new Settings { IsFirstRun = true });
    }

    /// <summary>Anzahl der <see cref="SaveAsync"/>-Aufrufe.</summary>
    public int SaveCallCount { get; private set; }

    /// <summary>Der zuletzt persistierte Autostart-Wert.</summary>
    public bool PersistedAutoStart => _settings.AutoStart;

    /// <inheritdoc />
    public Task<Settings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Clone(_settings));
    }

    /// <inheritdoc />
    public Task SaveAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = Clone(settings);
        SaveCallCount++;
        return Task.CompletedTask;
    }

    private static Settings Clone(Settings source) => new()
    {
        Language = source.Language,
        BreakModel = source.BreakModel,
        AutoStart = source.AutoStart,
        PauseOnIdle = source.PauseOnIdle,
        IdleThresholdMinutes = source.IdleThresholdMinutes,
        SuppressOnFullscreen = source.SuppressOnFullscreen,
        CustomDurations = source.CustomDurations,
        SoundEnabled = source.SoundEnabled,
        ReminderSound = source.ReminderSound,
        SoundVolumePercent = source.SoundVolumePercent,
        HotkeysEnabled = source.HotkeysEnabled,
        HotkeyStartBreak = source.HotkeyStartBreak,
        HotkeySkipOrSnooze = source.HotkeySkipOrSnooze,
        HotkeyToggleDnd = source.HotkeyToggleDnd,
        UpdateCheckEnabled = source.UpdateCheckEnabled,
        UpdateCheckFrequency = source.UpdateCheckFrequency,
        AutoUpdate = source.AutoUpdate,
        LastUpdateCheck = source.LastUpdateCheck,
        PendingUpdateVersion = source.PendingUpdateVersion,
        PendingUpdateSha256 = source.PendingUpdateSha256,
        DimScreenDuringBreak = source.DimScreenDuringBreak,
        DimBrightnessPercent = source.DimBrightnessPercent,
        PauseMediaDuringBreak = source.PauseMediaDuringBreak,
        ResumeMediaAfterBreak = source.ResumeMediaAfterBreak,
        DarkenAllScreens = source.DarkenAllScreens,
        BreakOverlayColor = source.BreakOverlayColor,
        IsFirstRun = source.IsFirstRun,
    };
}
