namespace LookAway.Core.Interfaces;

/// <summary>
/// Persistiert unbehandelte Exceptions als nachvollziehbare Crash-Berichte.
/// </summary>
/// <remarks>
/// Implementierungen müssen so robust sein, dass ein Fehler beim
/// Schreiben des Crash-Berichts nicht selbst zu einem Crash führt.
/// </remarks>
public interface ICrashReporter
{
    /// <summary>
    /// Schreibt eine Exception als Crash-Bericht (JSON). Schluckt eigene
    /// Fehler beim Schreiben, damit der bestehende Crash-Pfad nicht
    /// erneut geworfen wird.
    /// </summary>
    /// <param name="exception">Die unbehandelte Exception.</param>
    /// <param name="source">Quelle des Crashes (z. B. <c>AppDomain.UnhandledException</c>).</param>
    void Report(Exception exception, string source);

    /// <summary>
    /// Wahr, wenn seit dem letzten <see cref="MarkResolved"/>-Aufruf
    /// mindestens ein Crash-Bericht geschrieben wurde, der dem Benutzer
    /// noch nicht gezeigt wurde.
    /// </summary>
    bool HasUnresolvedCrashes();

    /// <summary>
    /// Markiert alle bestehenden Crash-Berichte als vom Benutzer
    /// gesehen. Löscht die Berichte nicht — sie bleiben für Diagnose
    /// erhalten und werden über die Retention abgeräumt.
    /// </summary>
    void MarkResolved();
}
