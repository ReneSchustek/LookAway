
namespace LookAway.App.Services;

/// <summary>
/// Was der <see cref="SplashPresenter"/> vom Startbild braucht: zeigen und schließen.
/// Die Trennung vom Fenster macht die Zeitregel ohne Oberfläche prüfbar.
/// </summary>
internal interface ISplashView
{
    /// <summary>Lädt das Logo und zeigt das Startbild.</summary>
    /// <returns>Eine Task, die abgeschlossen ist, sobald das Bild steht.</returns>
    Task ShowAsync();

    /// <summary>Schließt das Startbild.</summary>
    void Close();
}

/// <summary>
/// Zeigt das Startbild beim Programmstart und schließt es, sobald der Start durch ist —
/// frühestens aber nach <see cref="MinimumDisplayTime"/>.
/// </summary>
internal sealed class SplashPresenter
{
    /// <summary>
    /// Kürzeste Standzeit. Ein Start, der schneller fertig ist, ließe das Bild sonst nur
    /// aufblitzen; das wirkt wie ein Fehler, nicht wie ein Start.
    /// </summary>
    public static readonly TimeSpan MinimumDisplayTime = TimeSpan.FromSeconds(1.5);

    private readonly Func<ISplashView> _createView;
    private readonly Func<TimeSpan> _now;
    private readonly Func<TimeSpan, Task> _delay;
    private ISplashView? _view;
    private TimeSpan _shownAt;

    /// <summary>Presenter für den Programmstart: Systemlaufzeit als Uhr, echtes Warten.</summary>
    /// <param name="createView">Legt das Startbild an.</param>
    public SplashPresenter(Func<ISplashView> createView)
        : this(createView, static () => TimeSpan.FromMilliseconds(Environment.TickCount64), static delay => Task.Delay(delay))
    {
    }

    /// <summary>Presenter mit vorgegebener Uhr und Wartefunktion (für Tests).</summary>
    /// <param name="createView">Legt das Startbild an.</param>
    /// <param name="now">Monotone Uhr; nur Differenzen zählen.</param>
    /// <param name="delay">Wartet die verbleibende Standzeit ab.</param>
    internal SplashPresenter(Func<ISplashView> createView, Func<TimeSpan> now, Func<TimeSpan, Task> delay)
    {
        ArgumentNullException.ThrowIfNull(createView);
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(delay);
        _createView = createView;
        _now = now;
        _delay = delay;
    }

    /// <summary>Zeigt das Startbild. Ein zweiter Aufruf ändert nichts.</summary>
    /// <returns>Eine Task, die abgeschlossen ist, sobald das Bild steht.</returns>
    public async Task ShowAsync()
    {
        if (_view is not null)
        {
            return;
        }

        _view = _createView();
        await _view.ShowAsync().ConfigureAwait(true);
        _shownAt = _now();
    }

    /// <summary>
    /// Schließt das Startbild, nachdem es mindestens <see cref="MinimumDisplayTime"/> stand.
    /// Ohne Startbild oder beim zweiten Aufruf geschieht nichts.
    /// </summary>
    /// <returns>Eine Task, die abgeschlossen ist, sobald das Bild geschlossen ist.</returns>
    public async Task CloseAsync()
    {
        ISplashView? view = _view;
        if (view is null)
        {
            return;
        }

        // Vor dem Warten abräumen: Ein zweiter Aufruf in der Wartezeit schließt nicht doppelt.
        _view = null;
        TimeSpan remaining = RemainingDisplayTime(_now() - _shownAt);
        if (remaining > TimeSpan.Zero)
        {
            await _delay(remaining).ConfigureAwait(true);
        }

        view.Close();
    }

    /// <summary>Wie lange das Bild nach <paramref name="shownFor"/> noch stehen muss.</summary>
    /// <param name="shownFor">Bisherige Standzeit.</param>
    /// <returns>Die Restzeit bis zur Mindeststandzeit, nie negativ.</returns>
    internal static TimeSpan RemainingDisplayTime(TimeSpan shownFor)
        => shownFor >= MinimumDisplayTime ? TimeSpan.Zero : MinimumDisplayTime - shownFor;
}
