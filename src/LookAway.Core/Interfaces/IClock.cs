namespace LookAway.Core.Interfaces;

/// <summary>
/// Abstraktion über die Systemzeit. Wird vom Timer-Service verwendet,
/// damit Tests deterministische Zeitsteuerung über einen FakeClock
/// erhalten können, ohne <see cref="DateTime"/> direkt anzuzapfen.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Aktuelle Wallclock-Zeit (UTC). Robust gegenüber System-Sleep,
    /// weil das Betriebssystem die Wallclock-Uhr nach dem Aufwachen
    /// korrekt fortschreibt.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Monoton wachsende Zeit seit Start des Prozesses (Stopwatch-basiert).
    /// Springt nicht bei Zeitumstellungen, kann aber unter Windows während
    /// System-Sleep stehen bleiben — daher nur für Diagnostik geeignet.
    /// </summary>
    TimeSpan MonotonicElapsed { get; }
}
