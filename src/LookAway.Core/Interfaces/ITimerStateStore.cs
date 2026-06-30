using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Persistiert eine <see cref="TimerSnapshot"/>-Momentaufnahme, damit der laufende
/// Arbeits-Countdown einen Prozess-Neustart innerhalb derselben Windows-Sitzung
/// überdauern kann (z. B. bei einer Aktualisierung).
/// </summary>
public interface ITimerStateStore
{
    /// <summary>Speichert die Momentaufnahme (überschreibt eine vorhandene).</summary>
    /// <param name="snapshot">Die zu sichernde Momentaufnahme.</param>
    void Save(TimerSnapshot snapshot);

    /// <summary>
    /// Lädt die zuletzt gespeicherte Momentaufnahme, oder <see langword="null"/>,
    /// wenn keine vorhanden oder lesbar ist.
    /// </summary>
    /// <returns>Die Momentaufnahme oder <see langword="null"/>.</returns>
    TimerSnapshot? Load();

    /// <summary>Entfernt eine vorhandene Momentaufnahme (nach dem Verbrauch).</summary>
    void Clear();
}
