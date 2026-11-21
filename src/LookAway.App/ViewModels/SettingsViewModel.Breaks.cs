using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Entities;
using LookAway.Core.Enums;

namespace LookAway.App.ViewModels;

/// <summary>
/// Seiten „Ton“ und „Pausen-Aktionen“: Erinnerungston mit Vorschau, Abdunkeln und Medien. Das
/// Aussehen des Overlays steht im Teil <c>SettingsViewModel.Overlay.cs</c>.
/// </summary>
internal sealed partial class SettingsViewModel
{
    [ObservableProperty]
    public partial bool SoundEnabled { get; set; }

    [ObservableProperty]
    public partial SettingsOption<SoundType>? SelectedSoundOption { get; set; }

    [ObservableProperty]
    public partial int SoundVolume { get; set; }

    [ObservableProperty]
    public partial bool DimScreenDuringBreak { get; set; }

    [ObservableProperty]
    public partial int DimBrightnessPercent { get; set; }

    [ObservableProperty]
    public partial bool PauseMediaDuringBreak { get; set; }

    [ObservableProperty]
    public partial bool ResumeMediaAfterBreak { get; set; }

    [ObservableProperty]
    public partial bool DarkenAllScreens { get; set; }

    /// <summary>Auswählbare Erinnerungstöne mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<SoundType>> Sounds { get; private set; } = [];

    /// <summary>Der aktuell gewählte Erinnerungston.</summary>
    public SoundType SelectedSound => SelectedSoundOption?.Value ?? SoundType.Chime;

    /// <summary>Untere Grenze der Lautstärke.</summary>
    public int SoundVolumeMin => Settings.MinSoundVolumePercent;

    /// <summary>Obere Grenze der Lautstärke.</summary>
    public int SoundVolumeMax => Settings.MaxSoundVolumePercent;

    /// <summary>Untere Grenze der Pause-Helligkeit.</summary>
    public int DimBrightnessMin => Settings.MinDimBrightnessPercent;

    /// <summary>Obere Grenze der Pause-Helligkeit.</summary>
    public int DimBrightnessMax => Settings.MaxDimBrightnessPercent;

    /// <summary>Wählt den Erinnerungston anhand seines Werts (UI/Test-Hilfe).</summary>
    /// <param name="soundType">Zu wählender Ton.</param>
    public void SelectSound(SoundType soundType)
        => SelectedSoundOption = Sounds.First(option => option.Value == soundType);

    [RelayCommand]
    private void PreviewSound() => _soundService.Play(SelectedSound, SoundVolume);

    private void LoadBreakSettings(Settings settings)
    {
        SoundEnabled = settings.SoundEnabled;
        SelectSound(settings.ReminderSound);
        SoundVolume = settings.SoundVolumePercent;

        DimScreenDuringBreak = settings.DimScreenDuringBreak;
        DimBrightnessPercent = settings.DimBrightnessPercent;
        PauseMediaDuringBreak = settings.PauseMediaDuringBreak;
        ResumeMediaAfterBreak = settings.ResumeMediaAfterBreak;
        DarkenAllScreens = settings.DarkenAllScreens;
        LoadOverlaySettings(settings);
    }

    private void StoreBreakSettings(Settings settings)
    {
        settings.SoundEnabled = SoundEnabled;
        settings.ReminderSound = SelectedSound;
        settings.SoundVolumePercent = Math.Clamp(SoundVolume, SoundVolumeMin, SoundVolumeMax);
        settings.DimScreenDuringBreak = DimScreenDuringBreak;
        settings.DimBrightnessPercent = Math.Clamp(DimBrightnessPercent, DimBrightnessMin, DimBrightnessMax);
        settings.PauseMediaDuringBreak = PauseMediaDuringBreak;
        settings.ResumeMediaAfterBreak = ResumeMediaAfterBreak;
        settings.DarkenAllScreens = DarkenAllScreens;
        StoreOverlaySettings(settings);
    }
}
