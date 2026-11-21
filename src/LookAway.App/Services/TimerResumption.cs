using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Services;

/// <summary>
/// Setzt den Countdown nach einem Neustart in derselben Windows-Sitzung fort, etwa nach einer
/// Aktualisierung: Beim Beenden wird die Restzeit gesichert, beim Start genau einmal übernommen.
/// </summary>
/// <remarks>
/// Nach einem Windows-Neustart beginnt die Arbeitsphase neu; wer neu angemeldet ist, hat seine
/// Pause gehabt. Die Sitzung erkennt eine Marke aus Uhrzeit minus Laufzeit seit dem Systemstart.
/// </remarks>
internal sealed class TimerResumption
{
    private readonly ITimerStateStore _store;
    private readonly IClock _clock;
    private readonly Func<long> _millisecondsSinceBoot;

    /// <summary>Erzeugt die Fortsetzung über ihrer Ablage.</summary>
    /// <param name="store">Ablage der Momentaufnahme.</param>
    /// <param name="clock">Wanduhr für die Sitzungsmarke.</param>
    /// <param name="millisecondsSinceBoot">Laufzeit seit dem Systemstart, etwa <see cref="Environment.TickCount64"/>.</param>
    public TimerResumption(ITimerStateStore store, IClock clock, Func<long> millisecondsSinceBoot)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(millisecondsSinceBoot);
        _store = store;
        _clock = clock;
        _millisecondsSinceBoot = millisecondsSinceBoot;
    }

    /// <summary>
    /// Holt die gesicherte Restzeit und verwirft die Momentaufnahme, damit sie nur einmal greift.
    /// </summary>
    /// <param name="model">Pausenmodell, mit dem jetzt gestartet wird.</param>
    /// <returns>
    /// Die Restzeit, wenn die Aufnahme aus derselben Sitzung und zum selben Modell stammt und noch
    /// Zeit übrig war; sonst <c>null</c> für einen regulären Neubeginn.
    /// </returns>
    public TimeSpan? Take(BreakModel model)
    {
        TimerSnapshot? snapshot = _store.Load();
        if (snapshot is null)
        {
            return null;
        }

        _store.Clear();

        bool sameSession = SessionMarker.IsSameSession(snapshot.SessionMarker, CurrentMarker());
        return sameSession
            && snapshot.Model == model
            && snapshot.WorkRemaining > TimeSpan.Zero
            ? snapshot.WorkRemaining
            : null;
    }

    /// <summary>Sichert die Restzeit einer laufenden Arbeitsphase.</summary>
    /// <param name="model">Aktives Pausenmodell.</param>
    /// <param name="workRemaining">Restzeit bis zur nächsten Pause.</param>
    public void Remember(BreakModel model, TimeSpan workRemaining)
        => _store.Save(new TimerSnapshot(model, workRemaining, CurrentMarker()));

    private DateTimeOffset CurrentMarker() => SessionMarker.Compute(_clock.UtcNow, _millisecondsSinceBoot());
}
