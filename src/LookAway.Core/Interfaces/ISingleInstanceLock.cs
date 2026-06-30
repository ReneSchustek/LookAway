namespace LookAway.Core.Interfaces;

/// <summary>
/// Stellt sicher, dass pro Benutzer nur eine Instanz der Anwendung aktiv ist, und
/// erlaubt einer Zweit-Instanz, die laufende Instanz zu benachrichtigen
/// (z. B. um deren Fenster in den Vordergrund zu holen).
/// </summary>
public interface ISingleInstanceLock : IDisposable
{
    /// <summary>Wird ausgelöst, wenn eine Zweit-Instanz die Aktivierung angefordert hat.</summary>
    event EventHandler? ActivationRequested;

    /// <summary>Wahr, wenn dieser Prozess die Sperre hält (also die einzige Instanz ist).</summary>
    bool IsOwner { get; }

    /// <summary>
    /// Versucht, die Sperre zu übernehmen. Liefert <see langword="true"/>, wenn
    /// dieser Prozess die einzige Instanz ist; sonst <see langword="false"/>.
    /// </summary>
    /// <returns>Ob die Sperre übernommen wurde.</returns>
    bool TryAcquire();

    /// <summary>
    /// Signalisiert der bereits laufenden Instanz, dass sie sich zeigen soll.
    /// Wird von einer Zweit-Instanz aufgerufen, nachdem <see cref="TryAcquire"/>
    /// <see langword="false"/> geliefert hat.
    /// </summary>
    /// <returns>Ob das Signal zugestellt werden konnte.</returns>
    bool SignalExistingInstance();
}
