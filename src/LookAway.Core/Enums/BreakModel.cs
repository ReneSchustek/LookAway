namespace LookAway.Core.Enums;

/// <summary>
/// Verfuegbare Pausenmodelle der Anwendung.
/// Die konkreten Intervalle und Geschaeftsregeln werden in BRIEF005 / BRIEF006 implementiert;
/// hier wird das Enum als Platzhalter fuer die Persistenz angelegt.
/// </summary>
public enum BreakModel
{
    /// <summary>Klassisches Pomodoro: 25 min Arbeit / 5 min Pause.</summary>
    ClassicPomodoro,

    /// <summary>Modifiziertes Pomodoro: 50 min Arbeit / 10 min Pause.</summary>
    ModifiedPomodoro,

    /// <summary>Ultradianer Rhythmus: 90 min Arbeit / 20 min Pause.</summary>
    Ultradian,

    /// <summary>Mikro-Pausen fuer Koerperhaltung: 30-45 min Arbeit / 2 min Pause.</summary>
    PhysicalCounter,

    /// <summary>Aufgabenbasiert: Pause wird durch Ereignis ausgeloest, max. 120 min Arbeit.</summary>
    TaskBased,
}
