using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Localization;
using LookAway.Core.Services;
using LookAway.Core.ValueObjects;

namespace LookAway.App.ViewModels;

/// <summary>
/// Update-Belange des <see cref="SettingsViewModel"/>: Aktivierung/Auto-Update,
/// Prüf-Häufigkeit, manuelle Prüfung sowie die direkte Ein-Klick-Installation eines
/// gefundenen Updates. Laden/Speichern der übrigen Einstellungen liegen im Kern-ViewModel.
/// </summary>
internal sealed partial class SettingsViewModel
{
    [ObservableProperty]
    public partial bool UpdateCheckEnabled { get; set; }

    [ObservableProperty]
    public partial bool AutoUpdate { get; set; }

    [ObservableProperty]
    public partial SettingsOption<UpdateCheckFrequency>? SelectedFrequencyOption { get; set; }

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadAvailable))]
    public partial Uri? DownloadUri { get; set; }

    // Wahr, sobald die letzte Prüfung ein installierbares Paket gefunden hat; steuert
    // die Sichtbarkeit des "Jetzt installieren"-Buttons.
    [ObservableProperty]
    public partial bool IsUpdateInstallable { get; set; }

    // Letztes gefundenes Update; Grundlage für die direkte Installation, damit der
    // Benutzer nicht über die Release-Seite eine Datei suchen muss.
    private UpdateInfo? _lastUpdate;

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

    /// <summary>Download-Link-Text (Release-Seite als manueller Ausweg).</summary>
    public string UpdateDownloadLabel => _localization.GetText(SettingsTextKeys.UpdateDownload);

    /// <summary>Beschriftung des "Jetzt installieren"-Buttons.</summary>
    public string UpdateInstallNowLabel => _localization.GetText(SettingsTextKeys.UpdateInstallNow);

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

            // Direkte Installation nur anbieten, wenn ein Paket vorliegt (Portable-ZIP).
            // Die Release-Seite (DownloadUri) bleibt als manueller Ausweg sichtbar.
            _lastUpdate = info;
            IsUpdateInstallable = info.PackageUrl is not null;
        }
        else
        {
            UpdateStatusText = _localization.GetText(SettingsTextKeys.UpdateUpToDate);
            _lastUpdate = null;
            IsUpdateInstallable = false;
        }
    }

    /// <summary>
    /// Lädt das gefundene Update herunter, prüft seine Signatur und stellt es bereit.
    /// Eingespielt wird es — wie beim Auto-Update — verifiziert beim nächsten Start;
    /// so muss der Benutzer nicht selbst eine Datei von der Release-Seite holen.
    /// </summary>
    [RelayCommand]
    private async Task InstallUpdateAsync(CancellationToken cancellationToken)
    {
        if (_lastUpdate is not { } info)
        {
            return;
        }

        // Während Download/Entpacken keine erneute Installation zulassen und den
        // Fortschritt rückmelden.
        IsUpdateInstallable = false;
        UpdateStatusText = _localization.GetText(SettingsTextKeys.UpdateStaging);

        StagedUpdate? staged = await _updateInstaller
            .DownloadAndStageAsync(info, cancellationToken)
            .ConfigureAwait(true);
        if (staged is null)
        {
            // Fehlgeschlagen (Netz/Signatur/Entpacken): Button wieder anbieten und auf
            // den manuellen Ausweg (Release-Seite) hinweisen.
            UpdateStatusText = _localization.GetText(SettingsTextKeys.UpdateInstallFailed);
            IsUpdateInstallable = true;
            return;
        }

        // Version + Datei-Hash vermerken: Der Anwendungs-Bootstrap spielt das Paket
        // beim nächsten Start verifiziert ein (Datei-Tausch mit Backup/Rollback).
        Settings settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(true);
        settings.PendingUpdateVersion = staged.Version;
        settings.PendingUpdateSha256 = staged.ExecutableSha256;
        await _settingsRepository.SaveAsync(settings, cancellationToken).ConfigureAwait(true);

        UpdateStatusText = _localization.GetText(SettingsTextKeys.UpdateReady);
    }

    private List<SettingsOption<UpdateCheckFrequency>> BuildFrequencyOptions()
        => Enum.GetValues<UpdateCheckFrequency>()
            .Select(frequency => new SettingsOption<UpdateCheckFrequency>(
                frequency,
                () => _localization.GetText(SettingsTextKeys.ForFrequency(frequency))))
            .ToList();
}
