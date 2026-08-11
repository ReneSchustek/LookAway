using LookAway.Core.Exceptions;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für <see cref="AutoStartException"/>.
/// </summary>
/// <remarks>
/// Geworfen wird die Ausnahme in der Registry-Anbindung, also hinter der Systemgrenze,
/// die kein Test überschreitet. Was hier geprüft wird, ist der Teil, der davor liegt:
/// dass die Ausnahme die auslösende Ursache weiterreicht.
/// </remarks>
public sealed class AutoStartExceptionTests
{
    /// <remarks>
    /// Ohne die weitergereichte Ursache stünde im Protokoll nur „Autostart
    /// fehlgeschlagen" — und nicht, ob die Gruppenrichtlinie oder ein fehlender
    /// Programmpfad dahintersteckte.
    /// </remarks>
    [Fact]
    public void KeepsTheUnderlyingCause()
    {
        UnauthorizedAccessException cause = new("Zugriff verweigert.");

        AutoStartException exception = new("Eintragen fehlgeschlagen.", cause);

        Assert.Same(cause, exception.InnerException);
        Assert.Equal("Eintragen fehlgeschlagen.", exception.Message);
    }

    [Fact]
    public void KeepsTheMessageWithoutACause()
    {
        AutoStartException exception = new("Programmpfad nicht ermittelbar.");

        Assert.Null(exception.InnerException);
        Assert.Equal("Programmpfad nicht ermittelbar.", exception.Message);
    }

    /// <remarks>
    /// Der parameterlose Konstruktor wird nirgends aufgerufen; er ist vorhanden, weil
    /// die Analyse ihn für Ausnahmetypen verlangt. Der Test hält fest, dass er dabei
    /// nicht mit einer leeren Meldung endet.
    /// </remarks>
    [Fact]
    public void HasADefaultMessageWithoutArguments()
    {
        AutoStartException exception = new();

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }
}
