namespace LookAway.Core.Domain;

/// <summary>
/// Berechnet und vergleicht eine „Sitzungsmarke", mit der sich ein Neustart
/// innerhalb derselben Windows-Sitzung von einem echten System-Neustart
/// unterscheiden lässt.
/// </summary>
/// <remarks>
/// Die Marke ist der ungefähre Systemstart-Zeitpunkt (aktuelle Zeit minus
/// Laufzeit seit Systemstart). Innerhalb einer Sitzung bleibt sie nahezu konstant
/// (beide Größen laufen synchron); nach einem Windows-Neustart springt sie auf den
/// neuen Startzeitpunkt. Eine kleine Toleranz fängt Uhr-/NTP-Korrekturen ab.
/// </remarks>
public static class SessionMarker
{
    /// <summary>Toleranz beim Vergleich zweier Sitzungsmarken (gegen Uhr-/NTP-Drift).</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Berechnet die Sitzungsmarke aus der aktuellen Zeit und der Laufzeit seit
    /// Systemstart.
    /// </summary>
    /// <param name="utcNow">Aktuelle UTC-Zeit.</param>
    /// <param name="millisecondsSinceBoot">Millisekunden seit Systemstart (z. B. <see cref="Environment.TickCount64"/>).</param>
    /// <returns>Die Sitzungsmarke (ungefährer Systemstart-Zeitpunkt).</returns>
    public static DateTimeOffset Compute(DateTimeOffset utcNow, long millisecondsSinceBoot)
        => utcNow - TimeSpan.FromMilliseconds(millisecondsSinceBoot);

    /// <summary>
    /// Gehören zwei Sitzungsmarken (innerhalb der Toleranz) zur selben
    /// Windows-Sitzung?
    /// </summary>
    /// <param name="a">Erste Marke.</param>
    /// <param name="b">Zweite Marke.</param>
    /// <returns><see langword="true"/>, wenn dieselbe Sitzung.</returns>
    public static bool IsSameSession(DateTimeOffset a, DateTimeOffset b)
        => (a - b).Duration() <= Tolerance;
}
