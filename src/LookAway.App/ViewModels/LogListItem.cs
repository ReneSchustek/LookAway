using Microsoft.Extensions.Logging;

namespace LookAway.App.ViewModels;

/// <summary>
/// Ein Protokolleintrag, wie er in der Liste steht.
/// </summary>
/// <param name="Timestamp">Zeitpunkt des Eintrags.</param>
/// <param name="TimestampText">Der Zeitpunkt in Ortszeit, fertig formatiert.</param>
/// <param name="Level">Stufe des Eintrags.</param>
/// <param name="LevelText">Die Stufe als Wort — sie wird nie allein über Farbe ausgedrückt.</param>
/// <param name="Category">Herkunft des Eintrags.</param>
/// <param name="Message">Meldungstext.</param>
internal sealed record LogListItem(
    DateTimeOffset Timestamp,
    string TimestampText,
    LogLevel Level,
    string LevelText,
    string Category,
    string Message)
{
    /// <summary>Wahr bei Warnungen — steuert die Kennzeichnung in der Liste.</summary>
    public bool IsWarning => Level == LogLevel.Warning;

    /// <summary>Wahr bei Fehlern und schwerwiegenden Fehlern.</summary>
    public bool IsError => Level >= LogLevel.Error;

    /// <summary>Wahr bei allem darunter — Hinweise und Diagnose.</summary>
    public bool IsInformation => Level < LogLevel.Warning;
}
