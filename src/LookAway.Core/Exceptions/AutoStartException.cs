namespace LookAway.Core.Exceptions;

/// <summary>
/// Wird ausgelöst, wenn eine Autostart-Operation (Lesen, Eintragen oder
/// Entfernen) fehlschlägt — etwa weil eine Gruppenrichtlinie den Zugriff auf
/// den Run-Schlüssel sperrt oder der eigene Programmpfad nicht ermittelbar ist.
/// </summary>
/// <remarks>
/// Eine eigene Exception-Klasse statt einer generischen <see cref="Exception"/>
/// erlaubt es Aufrufern (Startup-Abgleich, Settings-UI), gezielt nur diese
/// Fehlerklasse abzufangen und den restlichen Programmlauf unberührt zu lassen.
/// </remarks>
public sealed class AutoStartException : Exception
{
    /// <summary>
    /// Erzeugt eine Exception ohne weitere Details.
    /// </summary>
    public AutoStartException()
    {
    }

    /// <summary>
    /// Erzeugt eine Exception mit erklärender Meldung.
    /// </summary>
    /// <param name="message">Beschreibung der fehlgeschlagenen Operation.</param>
    public AutoStartException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Erzeugt eine Exception mit Meldung und auslösender Ursache.
    /// </summary>
    /// <param name="message">Beschreibung der fehlgeschlagenen Operation.</param>
    /// <param name="innerException">Die zugrundeliegende Ausnahme.</param>
    public AutoStartException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
