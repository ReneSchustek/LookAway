namespace LookAway.Core.Interfaces;

/// <summary>
/// Abstraktion über System-Power-Events (Sleep/Resume).
/// Implementierungen verbinden sich an die Plattform (z. B.
/// <c>Microsoft.Win32.SystemEvents.PowerModeChanged</c>) und übersetzen
/// die Events in Domän-Events.
/// </summary>
public interface IPowerModeWatcher : IDisposable
{
    /// <summary>
    /// Wird gefeuert, wenn das System in den Standby/Hibernate geht.
    /// </summary>
    event EventHandler? Suspending;

    /// <summary>
    /// Wird gefeuert, wenn das System aus Standby/Hibernate aufwacht.
    /// </summary>
    event EventHandler? Resuming;

    /// <summary>
    /// Beginnt den Empfang der Plattform-Events. Mehrfaches Aufrufen
    /// ist idempotent.
    /// </summary>
    void Start();
}
