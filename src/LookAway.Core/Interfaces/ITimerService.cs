using System.Diagnostics.CodeAnalysis;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Domain-Service fuer Pausen-Erinnerungen. Reine Logik ohne UI- oder
/// Plattform-Abhaengigkeit. Lifecycle: Start → (Working ↔ OnBreak) ... → Stop.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Die Schnittstelle schreibt die Member 'Stop()' und 'Resume()' vor. Die Konflikte mit VB.NET-Schluesselwoertern werden in einer Single-Language-C#-Codebasis bewusst akzeptiert.")]
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
    /// Stream der vom Service ausgegebenen Domain-Events. Wird ueber einen
    /// Channel gespeist und ist deterministisch konsumierbar (z. B. via
    /// <c>await foreach</c>).
    /// </summary>
    IAsyncEnumerable<TimerEvent> Events { get; }

    /// <summary>Startet eine neue Arbeitsphase mit dem angegebenen Intervall.</summary>
    /// <param name="interval">Aktiv zu nutzendes Intervall.</param>
    void Start(BreakInterval interval);

    /// <summary>Beendet den Timer und kehrt in den Idle-Zustand zurueck.</summary>
    void Stop();

    /// <summary>Pausiert den Timer durch den Benutzer; Restzeit wird gemerkt.</summary>
    void Pause();

    /// <summary>Setzt einen pausierten Timer fort.</summary>
    void Resume();
}
