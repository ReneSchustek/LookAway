namespace LookAway.Core.Exceptions;

/// <summary>
/// Wird ausgeloest, wenn eine Autostart-Operation (Lesen, Eintragen oder
/// Entfernen) fehlschlaegt — etwa weil eine Gruppenrichtlinie den Zugriff auf
/// den Run-Schluessel sperrt oder der eigene Programmpfad nicht ermittelbar ist.
/// </summary>
/// <remarks>
/// Eine eigene Exception-Klasse statt einer generischen <see cref="Exception"/>
/// erlaubt es Aufrufern (Startup-Abgleich, Settings-UI), gezielt nur diese
/// Fehlerklasse abzufangen und den restlichen Programmlauf unberuehrt zu lassen.
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
    /// Erzeugt eine Exception mit erklaerender Meldung.
    /// </summary>
    /// <param name="message">Beschreibung der fehlgeschlagenen Operation.</param>
    public AutoStartException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Erzeugt eine Exception mit Meldung und ausloesender Ursache.
    /// </summary>
    /// <param name="message">Beschreibung der fehlgeschlagenen Operation.</param>
    /// <param name="innerException">Die zugrundeliegende Ausnahme.</param>
    public AutoStartException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
