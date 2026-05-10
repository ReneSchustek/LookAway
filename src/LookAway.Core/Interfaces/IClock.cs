namespace LookAway.Core.Interfaces;

/// <summary>
/// Abstraktion ueber die Systemzeit. Wird vom Timer-Service verwendet,
/// damit Tests deterministische Zeitsteuerung ueber einen FakeClock
/// erhalten koennen, ohne <see cref="DateTime"/> direkt anzuzapfen.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Aktuelle Wallclock-Zeit (UTC). Robust gegenueber System-Sleep,
    /// weil das Betriebssystem die Wallclock-Uhr nach dem Aufwachen
    /// korrekt fortschreibt.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Monoton wachsende Zeit seit Start des Prozesses (Stopwatch-basiert).
    /// Springt nicht bei Zeitumstellungen, kann aber unter Windows waehrend
    /// System-Sleep stehen bleiben — daher nur fuer Diagnostik geeignet.
    /// </summary>
    TimeSpan MonotonicElapsed { get; }
}
