using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Entities;

/// <summary>
/// Einstellungen, die eine Pause begleiten: Erinnerungston, globale Hotkeys, Update-Prüfung und die
/// Aktionen während der Pause (Abdunkeln, Medien). Das Aussehen des Overlays steht in
/// <c>Settings.Overlay.cs</c>.
/// </summary>
public sealed partial class Settings
{
    private SoundType _reminderSound = SoundType.Chime;
    private int _soundVolumePercent = 30;

    /// <summary>Untergrenze der Lautstärke in Prozent.</summary>
    public const int MinSoundVolumePercent = 0;

    /// <summary>Obergrenze der Lautstärke in Prozent.</summary>
    public const int MaxSoundVolumePercent = 100;

    /// <summary>
    /// Soll bei einer Pause-Erinnerung ein Ton abgespielt werden? Default: <c>false</c>.
    /// </summary>
    public bool SoundEnabled { get; set; }

    /// <summary>Gewählter Erinnerungston. Default: <see cref="SoundType.Chime"/>.</summary>
    public SoundType ReminderSound
    {
        get => _reminderSound;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unbekannter Sound-Typ.");
            }
            _reminderSound = value;
        }
    }

    /// <summary>
    /// Lautstärke des Erinnerungstons in Prozent (gültig
    /// <see cref="MinSoundVolumePercent"/>–<see cref="MaxSoundVolumePercent"/>). Default: 30.
    /// </summary>
    public int SoundVolumePercent
    {
        get => _soundVolumePercent;
        set
        {
            if (value is < MinSoundVolumePercent or > MaxSoundVolumePercent)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"SoundVolumePercent muss zwischen {MinSoundVolumePercent} und {MaxSoundVolumePercent} liegen.");
            }
            _soundVolumePercent = value;
        }
    }

    /// <summary>Sollen globale Hotkeys aktiv sein? Default: <c>true</c>.</summary>
    public bool HotkeysEnabled { get; set; } = true;

    /// <summary>Hotkey für "Pause starten".</summary>
    public HotkeyDefinition HotkeyStartBreak { get; set; } = HotkeyDefaults.StartBreak;

    /// <summary>Hotkey für "Überspringen/Snooze".</summary>
    public HotkeyDefinition HotkeySkipOrSnooze { get; set; } = HotkeyDefaults.SkipOrSnooze;

    /// <summary>Hotkey für "DND umschalten".</summary>
    public HotkeyDefinition HotkeyToggleDnd { get; set; } = HotkeyDefaults.ToggleDnd;

    private UpdateCheckFrequency _updateCheckFrequency = UpdateCheckFrequency.Weekly;

    /// <summary>Soll auf Updates geprüft werden? Default: <c>true</c>.</summary>
    public bool UpdateCheckEnabled { get; set; } = true;

    /// <summary>Häufigkeit der Update-Prüfung. Default: wöchentlich.</summary>
    public UpdateCheckFrequency UpdateCheckFrequency
    {
        get => _updateCheckFrequency;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unbekannte Update-Prüf-Häufigkeit.");
            }
            _updateCheckFrequency = value;
        }
    }

    /// <summary>
    /// Soll die neueste Version automatisch im Hintergrund heruntergeladen und
    /// beim nächsten Start installiert werden? Default: <c>false</c>.
    /// </summary>
    public bool AutoUpdate { get; set; }

    /// <summary>Zeitpunkt der letzten Update-Prüfung; <c>null</c>, wenn nie geprüft.</summary>
    public DateTimeOffset? LastUpdateCheck { get; set; }

    /// <summary>
    /// Version eines vom Programm selbst heruntergeladenen, noch einzuspielenden
    /// Updates; <c>null</c>, wenn keines aussteht. Dient zusammen mit
    /// <see cref="PendingUpdateSha256"/> als vertrauenswürdiger Nachweis, dass ein
    /// Staging-Ordner tatsächlich von dieser Installation stammt (Schutz vor
    /// untergeschobenen Ordnern).
    /// </summary>
    public string? PendingUpdateVersion { get; set; }

    /// <summary>
    /// SHA-256 (Hex) der heruntergeladenen Programmdatei des ausstehenden Updates;
    /// wird vor dem Einspielen mit der gestagten Datei verglichen.
    /// </summary>
    public string? PendingUpdateSha256 { get; set; }

    /// <summary>Untergrenze der Pause-Helligkeit in Prozent.</summary>
    /// <remarks>Ganz dunkel soll der Bildschirm nicht werden: Countdown und Hinweis müssen lesbar bleiben.</remarks>
    public const int MinDimBrightnessPercent = 10;

    /// <summary>Obergrenze der Pause-Helligkeit in Prozent.</summary>
    /// <remarks>Darüber ist das Dimmen kaum noch vom normalen Betrieb zu unterscheiden.</remarks>
    public const int MaxDimBrightnessPercent = 80;

    private int _dimBrightnessPercent = 30;

    /// <summary>Soll der Bildschirm während der Pause gedimmt werden? Default: <c>false</c>.</summary>
    public bool DimScreenDuringBreak { get; set; }

    /// <summary>
    /// Zielhelligkeit während der Pause in Prozent (gültig
    /// <see cref="MinDimBrightnessPercent"/>–<see cref="MaxDimBrightnessPercent"/>). Default: 30.
    /// </summary>
    public int DimBrightnessPercent
    {
        get => _dimBrightnessPercent;
        set
        {
            if (value is < MinDimBrightnessPercent or > MaxDimBrightnessPercent)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"DimBrightnessPercent muss zwischen {MinDimBrightnessPercent} und {MaxDimBrightnessPercent} liegen.");
            }
            _dimBrightnessPercent = value;
        }
    }

    /// <summary>
    /// Sollen während der Pause alle angeschlossenen Bildschirme abgedunkelt
    /// werden (ein Overlay je Monitor)? Bei <c>false</c> nur der Hauptbildschirm.
    /// Default: <c>true</c>.
    /// </summary>
    public bool DarkenAllScreens { get; set; } = true;

    /// <summary>Soll die Medienwiedergabe während der Pause pausiert werden? Default: <c>false</c>.</summary>
    public bool PauseMediaDuringBreak { get; set; }

    /// <summary>Sollen Medien nach der Pause fortgesetzt werden? Default: <c>true</c>.</summary>
    public bool ResumeMediaAfterBreak { get; set; } = true;
}
