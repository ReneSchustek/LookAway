using System.Diagnostics.CodeAnalysis;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Domain-Service für Pausen-Erinnerungen. Reine Logik ohne UI- oder
/// Plattform-Abhängigkeit. Lifecycle: Start → (Working ↔ OnBreak) ... → Stop.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Die Member 'Stop()' und 'Resume()' sind bewusst so benannt; die Konflikte mit VB.NET-Schlüsselwörtern werden in einer Single-Language-C#-Codebasis akzeptiert.")]
public interface ITimerService
{
    /// <summary>Aktueller Zustand der State-Machine.</summary>
    TimerState State { get; }

    /// <summary>Aktiv konfiguriertes Intervall, oder <c>null</c> im Idle-Zustand.</summary>
    BreakInterval? CurrentInterval { get; }

    /// <summary>
    /// Verbleibende Zeit in der laufenden Phase.
    /// Im <see cref="TimerState.Idle"/>-Zustand <see cref="TimeSpan.Zero"/>.
    /// Im <see cref="TimerState.Paused"/>-Zustand die zur Pause-Zeit
    /// gespeicherte Restzeit.
    /// </summary>
    TimeSpan Remaining { get; }

    /// <summary>
    /// Stream der vom Service ausgegebenen Domain-Events. Wird über einen
    /// Channel gespeist und ist deterministisch konsumierbar (z. B. via
    /// <c>await foreach</c>).
    /// </summary>
    IAsyncEnumerable<TimerEvent> Events { get; }

    /// <summary>Startet eine neue Arbeitsphase mit dem angegebenen Intervall.</summary>
    /// <param name="interval">Aktiv zu nutzendes Intervall.</param>
    void Start(BreakInterval interval);

    /// <summary>Beendet den Timer und kehrt in den Idle-Zustand zurück.</summary>
    void Stop();

    /// <summary>Pausiert den Timer durch den Benutzer; Restzeit wird gemerkt.</summary>
    void Pause();

    /// <summary>Setzt einen pausierten Timer fort.</summary>
    void Resume();

    /// <summary>
    /// Setzt den Timer nach einer automatischen Abwesenheit (z. B. Inaktivität)
    /// fort. War die Abwesenheit mindestens so lang wie eine Pause, wird
    /// stattdessen eine frische Arbeitsphase gestartet — der Nutzer hat in dieser
    /// Zeit ohnehin nicht auf den Bildschirm geschaut, die Augen haben also bereits
    /// pausiert. Betrifft nur automatische Pausen, keine Benutzer-Pausen.
    /// </summary>
    /// <param name="awayDuration">Gesamtdauer der Abwesenheit vom Bildschirm.</param>
    void ResumeAfterAway(TimeSpan awayDuration);
}
