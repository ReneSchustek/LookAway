namespace LookAway.Core.Interfaces;

/// <summary>
/// Abstraktion ueber System-Power-Events (Sleep/Resume).
/// Implementierungen verbinden sich an die Plattform (z. B.
/// <c>Microsoft.Win32.SystemEvents.PowerModeChanged</c>) und uebersetzen
/// die Events in Domaen-Events.
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
