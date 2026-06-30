using System.Text.Json.Serialization;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Entities;

/// <summary>
/// Benutzerkonfiguration der Anwendung. Wird pro Windows-Benutzer
/// in <c>%APPDATA%\LookAway\settings.json</c> persistiert.
/// </summary>
/// <remarks>
/// Validierung erfolgt in den Property-Settern. Damit erkennt sowohl die
/// programmatische Zuweisung als auch die JSON-Deserialisierung ungueltige
/// Werte sofort und nicht erst beim Speichern.
/// </remarks>
public sealed class Settings
{
    private Language _language = Language.German;
    private BreakModel _breakModel = BreakModel.ClassicPomodoro;

    /// <summary>
    /// Anzeigesprache der Anwendung. Default: <see cref="Language.German"/>.
    /// </summary>
    public Language Language
    {
        get => _language;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unbekannter Sprachwert.");
            }
            _language = value;
        }
    }

    /// <summary>
    /// Aktives Pausenmodell. Default: <see cref="BreakModel.ClassicPomodoro"/>.
    /// </summary>
    public BreakModel BreakModel
    {
        get => _breakModel;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unbekanntes Pausenmodell.");
            }
            _breakModel = value;
        }
    }

    /// <summary>
    /// Soll die Anwendung beim Windows-Login automatisch starten?
    /// </summary>
    public bool AutoStart { get; set; }

    /// <summary>Untergrenze der Inaktivitaetsschwelle in Minuten.</summary>
    public const int MinIdleThresholdMinutes = 1;

    /// <summary>Obergrenze der Inaktivitaetsschwelle in Minuten.</summary>
    public const int MaxIdleThresholdMinutes = 30;

    private int _idleThresholdMinutes = 5;

    /// <summary>
    /// Soll der Timer bei laengerer Inaktivitaet automatisch pausieren?
    /// Default: <c>true</c>.
    /// </summary>
    public bool PauseOnIdle { get; set; } = true;

    /// <summary>
    /// Inaktivitaetsschwelle in Minuten (gueltig
    /// <see cref="MinIdleThresholdMinutes"/>–<see cref="MaxIdleThresholdMinutes"/>).
    /// Default: 5.
    /// </summary>
    public int IdleThresholdMinutes
    {
        get => _idleThresholdMinutes;
        set
        {
            if (value is < MinIdleThresholdMinutes or > MaxIdleThresholdMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"IdleThresholdMinutes muss zwischen {MinIdleThresholdMinutes} und {MaxIdleThresholdMinutes} liegen.");
            }
            _idleThresholdMinutes = value;
        }
    }

    /// <summary>
    /// Sollen Pause-Erinnerungen unterdrueckt werden, solange eine Vollbild-App
    /// laeuft (DND)? Default: <c>true</c>.
    /// </summary>
    public bool SuppressOnFullscreen { get; set; } = true;

    /// <summary>
    /// Optionale Benutzer-Ueberschreibung der Standarddauern. <c>null</c>,
    /// wenn die Defaults des aktiven Pausenmodells gelten sollen.
    /// </summary>
    public CustomDurations? CustomDurations { get; set; }

    private SoundType _reminderSound = SoundType.Chime;
    private int _soundVolumePercent = 30;

    /// <summary>Untergrenze der Lautstaerke in Prozent.</summary>
    public const int MinSoundVolumePercent = 0;

    /// <summary>Obergrenze der Lautstaerke in Prozent.</summary>
    public const int MaxSoundVolumePercent = 100;

    /// <summary>
    /// Soll bei einer Pause-Erinnerung ein Ton abgespielt werden? Default: <c>false</c>.
    /// </summary>
    public bool SoundEnabled { get; set; }

    /// <summary>Gewaehlter Erinnerungston. Default: <see cref="SoundType.Chime"/>.</summary>
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
    /// Lautstaerke des Erinnerungstons in Prozent (gueltig
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

    /// <summary>Hotkey fuer "Pause starten".</summary>
    public HotkeyDefinition HotkeyStartBreak { get; set; } = HotkeyDefaults.StartBreak;

    /// <summary>Hotkey fuer "Ueberspringen/Snooze".</summary>
    public HotkeyDefinition HotkeySkipOrSnooze { get; set; } = HotkeyDefaults.SkipOrSnooze;

    /// <summary>Hotkey fuer "DND umschalten".</summary>
    public HotkeyDefinition HotkeyToggleDnd { get; set; } = HotkeyDefaults.ToggleDnd;

    private UpdateCheckFrequency _updateCheckFrequency = UpdateCheckFrequency.Weekly;

    /// <summary>Soll auf Updates geprueft werden? Default: <c>true</c>.</summary>
    public bool UpdateCheckEnabled { get; set; } = true;

    /// <summary>Haeufigkeit der Update-Pruefung. Default: woechentlich.</summary>
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
                    "Unbekannte Update-Pruef-Haeufigkeit.");
            }
            _updateCheckFrequency = value;
        }
    }

    /// <summary>
    /// Soll die neueste Version automatisch im Hintergrund heruntergeladen und
    /// beim naechsten Start installiert werden? Default: <c>false</c>.
    /// </summary>
    public bool AutoUpdate { get; set; }

    /// <summary>Zeitpunkt der letzten Update-Pruefung; <c>null</c>, wenn nie geprueft.</summary>
    public DateTimeOffset? LastUpdateCheck { get; set; }

    /// <summary>Untergrenze der Pause-Helligkeit in Prozent.</summary>
    public const int MinDimBrightnessPercent = 10;

    /// <summary>Obergrenze der Pause-Helligkeit in Prozent.</summary>
    public const int MaxDimBrightnessPercent = 80;

    private int _dimBrightnessPercent = 30;

    /// <summary>Soll der Bildschirm waehrend der Pause gedimmt werden? Default: <c>false</c>.</summary>
    public bool DimScreenDuringBreak { get; set; }

    /// <summary>
    /// Zielhelligkeit waehrend der Pause in Prozent (gueltig
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
    /// Standardfarbe des Pausen-Overlays im ARGB-Hex-Format. Dunkel, leicht
    /// transparent (Alpha <c>F2</c>).
    /// </summary>
    public const string DefaultBreakOverlayColor = HexColor.Default;

    private string _breakOverlayColor = DefaultBreakOverlayColor;

    /// <summary>
    /// Sollen waehrend der Pause alle angeschlossenen Bildschirme abgedunkelt
    /// werden (ein Overlay je Monitor)? Bei <c>false</c> nur der Hauptbildschirm.
    /// Default: <c>true</c>.
    /// </summary>
    public bool DarkenAllScreens { get; set; } = true;

    /// <summary>
    /// Hintergrundfarbe des Pausen-Overlays als Hex-Zeichenkette im Format
    /// <c>#RRGGBB</c> oder <c>#AARRGGBB</c> (Alpha steuert die Transparenz).
    /// Default: <see cref="DefaultBreakOverlayColor"/>.
    /// </summary>
    public string BreakOverlayColor
    {
        get => _breakOverlayColor;
        set
        {
            if (!HexColor.IsValid(value))
            {
                throw new ArgumentException(
                    "BreakOverlayColor muss im Format #RRGGBB oder #AARRGGBB vorliegen.",
                    nameof(value));
            }
            _breakOverlayColor = value;
        }
    }

    /// <summary>Soll die Medienwiedergabe waehrend der Pause pausiert werden? Default: <c>false</c>.</summary>
    public bool PauseMediaDuringBreak { get; set; }

    /// <summary>Sollen Medien nach der Pause fortgesetzt werden? Default: <c>true</c>.</summary>
    public bool ResumeMediaAfterBreak { get; set; } = true;

    /// <summary>
    /// Wahr, wenn beim letzten Lesevorgang keine Konfigurationsdatei vorhanden war.
    /// Wird vom Repository gesetzt und nicht persistiert.
    /// </summary>
    [JsonIgnore]
    public bool IsFirstRun { get; set; }
}
