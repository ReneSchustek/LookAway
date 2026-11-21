using System.Text.Json.Serialization;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Entities;

/// <summary>
/// Benutzerkonfiguration der Anwendung. Liegt je Windows-Benutzer als
/// <c>settings.json</c> im Datenverzeichnis (<see cref="AppPaths"/>), im
/// Portable-Modus also neben der EXE.
/// </summary>
/// <remarks>
/// Validierung erfolgt in den Property-Settern. Damit erkennt sowohl die
/// programmatische Zuweisung als auch die JSON-Deserialisierung ungültige
/// Werte sofort und nicht erst beim Speichern. Grundeinstellungen stehen hier, alles, was eine
/// Pause begleitet (Ton, Hotkeys, Update, Pausen-Aktionen), in <c>Settings.BreakCompanions.cs</c>.
/// </remarks>
public sealed partial class Settings
{
    /// <summary>
    /// Pausenmodell, mit dem eine neue Konfiguration beginnt und das der Einrichtungsassistent
    /// vorschlägt. Das modifizierte Pomodoro (50/10) lässt längere Konzentrationsblöcke zu als
    /// das klassische und ist damit für den Einstieg die weniger störende Wahl.
    /// </summary>
    public const BreakModel DefaultBreakModel = BreakModel.ModifiedPomodoro;

    private Language _language = Language.German;
    private BreakModel _breakModel = DefaultBreakModel;
    private AppTheme _appTheme = AppTheme.System;

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
    /// Aktives Pausenmodell. Default: <see cref="DefaultBreakModel"/>.
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
    /// Erscheinungsbild der Fenster. Default: <see cref="AppTheme.System"/> —
    /// die Anwendung folgt Windows, solange der Benutzer nichts anderes wählt.
    /// </summary>
    public AppTheme AppTheme
    {
        get => _appTheme;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unbekanntes Erscheinungsbild.");
            }
            _appTheme = value;
        }
    }

    /// <summary>
    /// Soll die Anwendung beim Windows-Login automatisch starten?
    /// </summary>
    public bool AutoStart { get; set; }

    /// <summary>Untergrenze der Inaktivitätsschwelle in Minuten.</summary>
    public const int MinIdleThresholdMinutes = 1;

    /// <summary>Obergrenze der Inaktivitätsschwelle in Minuten.</summary>
    /// <remarks>
    /// Wer länger als eine halbe Stunde nichts tut, hat Pause gemacht; eine höhere
    /// Schwelle ließe den Countdown über die Mittagspause weiterlaufen.
    /// </remarks>
    public const int MaxIdleThresholdMinutes = 30;

    private int _idleThresholdMinutes = 5;

    /// <summary>
    /// Soll der Timer bei längerer Inaktivität automatisch pausieren?
    /// Default: <c>true</c>.
    /// </summary>
    public bool PauseOnIdle { get; set; } = true;

    /// <summary>
    /// Inaktivitätsschwelle in Minuten (gültig
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

    /// <summary>Untergrenze der Auto-Pausenstart-Verzögerung in Sekunden.</summary>
    /// <remarks>Kürzer reicht nicht, um die Erinnerung zu lesen und noch „5 Min später“ zu wählen.</remarks>
    public const int MinAutoStartBreakSeconds = 15;

    /// <summary>Obergrenze der Auto-Pausenstart-Verzögerung in Sekunden (3 Minuten).</summary>
    public const int MaxAutoStartBreakSeconds = 180;

    /// <summary>Schrittweite der Auto-Pausenstart-Verzögerung in Sekunden; feiner unterscheidet am Schieberegler niemand.</summary>
    public const int AutoStartBreakSecondsStep = 5;

    private int _autoStartBreakSeconds = 15;

    /// <summary>
    /// Soll die Pause automatisch starten, wenn der Benutzer die Erinnerung nach
    /// <see cref="AutoStartBreakSeconds"/> Sekunden nicht bedient? Ist dies
    /// <c>false</c>, bleibt die Erinnerung offen, bis der Benutzer eine Aktion wählt.
    /// Default: <c>true</c>.
    /// </summary>
    public bool AutoStartBreakEnabled { get; set; } = true;

    /// <summary>
    /// Verzögerung in Sekunden, nach der die Pause ohne Benutzeraktion automatisch
    /// startet (gültig <see cref="MinAutoStartBreakSeconds"/>–<see cref="MaxAutoStartBreakSeconds"/>).
    /// Nur wirksam, wenn <see cref="AutoStartBreakEnabled"/>. Default: 15.
    /// </summary>
    public int AutoStartBreakSeconds
    {
        get => _autoStartBreakSeconds;
        set
        {
            if (value is < MinAutoStartBreakSeconds or > MaxAutoStartBreakSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"AutoStartBreakSeconds muss zwischen {MinAutoStartBreakSeconds} und {MaxAutoStartBreakSeconds} liegen.");
            }
            _autoStartBreakSeconds = value;
        }
    }

    /// <summary>
    /// Sollen Pause-Erinnerungen unterdrückt werden, solange eine Vollbild-App
    /// läuft (DND)? Default: <c>true</c>.
    /// </summary>
    public bool SuppressOnFullscreen { get; set; } = true;

    /// <summary>
    /// Optionale Benutzer-Überschreibung der Standarddauern. <c>null</c>,
    /// wenn die Defaults des aktiven Pausenmodells gelten sollen.
    /// </summary>
    public CustomDurations? CustomDurations { get; set; }

    /// <summary>
    /// Wahr, wenn beim letzten Lesevorgang keine Konfigurationsdatei vorhanden war.
    /// Wird vom Repository gesetzt und nicht persistiert.
    /// </summary>
    [JsonIgnore]
    public bool IsFirstRun { get; set; }
}
