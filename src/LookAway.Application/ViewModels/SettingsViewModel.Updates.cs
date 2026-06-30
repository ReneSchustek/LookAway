using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Application.Localization;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Update-Belange des <see cref="SettingsViewModel"/>: Aktivierung/Auto-Update,
/// Prüf-Häufigkeit, manuelle Prüfung und der daraus abgeleitete Status- und
/// Download-Zustand. Laden und Speichern liegen im Kern-ViewModel.
/// </summary>
public sealed partial class SettingsViewModel
{
    [ObservableProperty]
    private bool _updateCheckEnabled;

    [ObservableProperty]
    private bool _autoUpdate;

    [ObservableProperty]
    private SettingsOption<UpdateCheckFrequency>? _selectedFrequencyOption;

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadAvailable))]
    private Uri? _downloadUri;

    /// <summary>Auswählbare Update-Prüf-Häufigkeiten mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<UpdateCheckFrequency>> UpdateFrequencies { get; private set; } = [];

    /// <summary>Die aktuell gewählte Prüf-Häufigkeit.</summary>
    public UpdateCheckFrequency SelectedFrequency => SelectedFrequencyOption?.Value ?? UpdateCheckFrequency.Weekly;

    /// <summary>Wahr, wenn ein Download-Link vorliegt.</summary>
    public bool IsDownloadAvailable => DownloadUri is not null;

    /// <summary>Beschriftung "Auf Updates prüfen".</summary>
    public string UpdateEnableLabel => _localization.GetText(SettingsTextKeys.UpdateEnableLabel);

    /// <summary>Beschriftung der Auto-Update-Option.</summary>
    public string AutoUpdateLabel => _localization.GetText(SettingsTextKeys.UpdateAutoLabel);

    /// <summary>Hinweistext zur Auto-Update-Option.</summary>
    public string AutoUpdateHint => _localization.GetText(SettingsTextKeys.UpdateAutoHint);

    /// <summary>Beschriftung der Prüf-Häufigkeit.</summary>
    public string UpdateFrequencyLabel => _localization.GetText(SettingsTextKeys.UpdateFrequencyLabel);

    /// <summary>Beschriftung des "Jetzt prüfen"-Buttons.</summary>
    public string UpdateCheckNowLabel => _localization.GetText(SettingsTextKeys.UpdateCheckNow);

    /// <summary>Download-Link-Text.</summary>
    public string UpdateDownloadLabel => _localization.GetText(SettingsTextKeys.UpdateDownload);

    /// <summary>Wählt die Prüf-Häufigkeit anhand ihres Werts (UI/Test-Hilfe).</summary>
    /// <param name="frequency">Zu wählende Häufigkeit.</param>
    public void SelectFrequency(UpdateCheckFrequency frequency)
        => SelectedFrequencyOption = UpdateFrequencies.First(option => option.Value == frequency);

    [RelayCommand]
    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        UpdateStatusText = _localization.GetText(SettingsTextKeys.UpdateChecking);
        DownloadUri = null;

        UpdateInfo info = await _updateChecker.CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);

        // Letzten Prüfzeitpunkt persistieren.
        Settings settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(true);
        settings.LastUpdateCheck = DateTimeOffset.UtcNow;
        await _settingsRepository.SaveAsync(settings, cancellationToken).ConfigureAwait(true);

        if (info.IsUpdateAvailable)
        {
            UpdateStatusText = string.Format(
                CultureInfo.CurrentCulture,
                _localization.GetText(SettingsTextKeys.UpdateAvailable),
                info.LatestVersion);
            DownloadUri = info.DownloadUrl;
        }
        else
        {
            UpdateStatusText = _localization.GetText(SettingsTextKeys.UpdateUpToDate);
        }
    }

    private List<SettingsOption<UpdateCheckFrequency>> BuildFrequencyOptions()
        => Enum.GetValues<UpdateCheckFrequency>()
            .Select(frequency => new SettingsOption<UpdateCheckFrequency>(
                frequency,
                () => _localization.GetText(SettingsTextKeys.ForFrequency(frequency))))
            .ToList();
}
