namespace LookAway.Core.Enums;

/// <summary>
/// Verfuegbare Pausenmodelle der Anwendung. Konkrete Standardintervalle
/// liefert <see cref="LookAway.Core.Domain.BreakModelRegistry"/>.
/// </summary>
public enum BreakModel
{
    /// <summary>Kurze Pausen: 60 min Arbeit / 5 min Pause.</summary>
    ShortBreaks,

    /// <summary>Klassisches Pomodoro: 25 min Arbeit / 5 min Pause.</summary>
    ClassicPomodoro,

    /// <summary>Modifiziertes Pomodoro: 50 min Arbeit / 10 min Pause.</summary>
    ModifiedPomodoro,

    /// <summary>Ultradianer Rhythmus: 90 min Arbeit / 20 min Pause.</summary>
    Ultradian,

    /// <summary>Mikro-Pausen fuer Koerperhaltung: 40 min Arbeit / 2 min Pause.</summary>
    PhysicalCounter,

    /// <summary>Aufgabenbasiert: Pause wird durch Ereignis ausgeloest, max. 120 min Arbeit.</summary>
    TaskBased,

    /// <summary>Gesetzliche Empfehlung Bildschirmarbeit: 120 min Arbeit / 15 min Pause.</summary>
    LegalCompliance,
}
