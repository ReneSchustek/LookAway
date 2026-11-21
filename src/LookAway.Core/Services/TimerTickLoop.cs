namespace LookAway.Core.Services;

/// <summary>
/// Der Hintergrund-Takt des Timers: ruft in festem Abstand eine Aktion auf, bis er abgebrochen
/// wird. Ein unerwarteter Fehler beendet ihn kontrolliert und wird gemeldet, statt als
/// stehengebliebene Uhr aufzufallen.
/// </summary>
/// <remarks>
/// Nicht threadsicher: Der <see cref="TimerService"/> startet, bricht ab und entsorgt nur unter
/// seiner Sperre. So trifft kein paralleler Start eine bereits entsorgte Abbruchquelle.
/// </remarks>
internal sealed class TimerTickLoop : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Action _tick;
    private readonly Action<Exception> _onCrash;
    private CancellationTokenSource? _cts;
    private Task? _task;

    /// <summary>Erzeugt den Takt, ohne ihn zu starten.</summary>
    /// <param name="interval">Abstand zwischen zwei Aufrufen.</param>
    /// <param name="tick">Wird je Takt aufgerufen.</param>
    /// <param name="onCrash">Erhält einen unerwarteten Fehler, bevor der Takt endet.</param>
    public TimerTickLoop(TimeSpan interval, Action tick, Action<Exception> onCrash)
    {
        _interval = interval;
        _tick = tick;
        _onCrash = onCrash;
    }

    /// <summary>Startet den Takt, falls er nicht schon läuft.</summary>
    public void EnsureRunning()
    {
        // Nur weiterlaufen lassen, wenn der Loop aktiv UND nicht bereits abgebrochen
        // ist (sonst würde ein Start nach Stop keinen neuen Loop aufsetzen).
        if (_task is { IsCompleted: false } && _cts is { IsCancellationRequested: false })
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        // Token lokal festhalten: ein zwischenzeitliches Stop+Start darf den gerade
        // gestarteten Loop nicht mit dem Token einer neuen Quelle laufen lassen.
        CancellationToken token = _cts.Token;
        _task = Task.Run(() => RunAsync(token));
    }

    /// <summary>Bricht den Takt ab; entsorgt wird die Quelle erst beim Neustart oder Entsorgen.</summary>
    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Bereits freigegeben (paralleler Stop/Dispose) — kein Handlungsbedarf.
        }
    }

    /// <summary>Bricht den Takt ab und gibt die Abbruchquelle frei.</summary>
    public void Dispose()
    {
        Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
                _tick();
            }
        }
        catch (OperationCanceledException)
        {
            // erwarteter Beendigungspfad
        }
        // Der Loop trägt den ganzen Pausenzyklus. Endet er an einem unerwarteten Fehler,
        // soll das im Protokoll stehen. Der Filter meldet, bevor der Stapel abgebaut ist.
        catch (Exception ex) when (ReportCrash(ex))
        {
            // Der Filter hat bereits gemeldet; der Loop terminiert kontrolliert.
        }
    }

    private bool ReportCrash(Exception exception)
    {
        _onCrash(exception);
        return true;
    }
}
