using System.Text.Json.Serialization;
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

    /// <summary>
    /// Optionale Benutzer-Ueberschreibung der Standarddauern. <c>null</c>,
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
