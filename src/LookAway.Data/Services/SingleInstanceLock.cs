using System.Globalization;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Lässt je Windows-Benutzer nur eine LookAway-Instanz laufen. Verwendet
/// einen benannten <see cref="Mutex"/> im
/// <c>Local\</c>-Namespace und einen <see cref="EventWaitHandle"/> als
/// minimaler IPC, damit eine Zweit-Instanz die laufende Instanz benachrichtigen
/// kann (z. B. um das Settings-Fenster anzuzeigen).
/// </summary>
public sealed class SingleInstanceLock : ISingleInstanceLock
{
    /// <summary>Wird gefeuert, wenn eine Zweit-Instanz signalisiert hat.</summary>
    public event EventHandler? ActivationRequested;

    /// <inheritdoc />
    /// <remarks>
    /// Das Setup setzt das Ereignis <c>Local\LookAway-Quit-&lt;Benutzer&gt;</c> selbst und wartet, bis der
    /// Mutex frei ist. Die Instanz beendet sich dann wie über das Tray-Menü, sichert also Countdown
    /// und Protokoll, statt vom Installer hart beendet zu werden. Eine Instanz vor 1.5 legt das
    /// Ereignis nicht an; das Setup erkennt so, dass es sie nicht beenden kann.
    /// </remarks>
    public event EventHandler? QuitRequested;

    private readonly string _mutexName;
    private readonly string _eventName;
    private readonly string _quitEventName;
    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
    private EventWaitHandle? _quitEvent;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private bool _ownsMutex;
    private bool _disposed;

    /// <summary>Erzeugt einen Lock für den angegebenen Benutzer.</summary>
    /// <param name="userName">Benutzername für den lokalen Namespace.</param>
    public SingleInstanceLock(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        _mutexName = string.Create(CultureInfo.InvariantCulture, $@"Local\LookAway-{userName}");
        _eventName = string.Create(CultureInfo.InvariantCulture, $@"Local\LookAway-Activate-{userName}");
        _quitEventName = string.Create(CultureInfo.InvariantCulture, $@"Local\LookAway-Quit-{userName}");
    }

    /// <summary>
    /// True, wenn der aktuelle Prozess den Mutex hält (also die einzige
    /// Instanz ist).
    /// </summary>
    public bool IsOwner => _ownsMutex;

    /// <summary>
    /// Versucht, die Single-Instance-Sperre zu übernehmen. Liefert
    /// <c>true</c>, wenn dieser Prozess die einzige Instanz ist;
    /// <c>false</c>, wenn bereits eine andere Instanz läuft.
    /// Wird der Lock übernommen, startet ein Hintergrund-Listener für
    /// Activation-Requests einer eventuellen Zweit-Instanz.
    /// </summary>
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _mutex = new Mutex(initiallyOwned: true, name: _mutexName, createdNew: out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _ownsMutex = true;
        _activationEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, _eventName);
        _quitEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, _quitEventName);
        StartActivationListener();
        return true;
    }

    /// <summary>
    /// Signalisiert der bereits laufenden Instanz, dass diese sich
    /// zeigen soll (z. B. Settings-Fenster öffnen). Wird von einer
    /// Zweit-Instanz aufgerufen, nachdem <see cref="TryAcquire"/>
    /// <c>false</c> zurückgegeben hat.
    /// </summary>
    public bool SignalExistingInstance()
    {
        try
        {
            using EventWaitHandle existing = EventWaitHandle.OpenExisting(_eventName);
            return existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gibt den Mutex und alle verbundenen Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopActivationListener();
        _listenerCts?.Dispose();
        _activationEvent?.Dispose();
        _quitEvent?.Dispose();
        ReleaseMutex();
    }

    private void StopActivationListener()
    {
        try
        {
            _listenerCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Handle bereits freigegeben — beim Herunterfahren unkritisch.
        }

        try
        {
            // Der Listener wacht auf das Abbruch-Handle sofort auf; die Sekunde ist nur
            // die Grenze, falls er hängt, damit das Beenden nicht an ihm stehenbleibt.
            _ = _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Ein Fehler des Listeners beim Herunterfahren ändert am Beenden nichts mehr.
        }
    }

    private void ReleaseMutex()
    {
        if (_mutex is null)
        {
            return;
        }

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex bereits freigegeben oder Thread-Affinitäts-Verletzung — tolerieren.
            }
        }

        _mutex.Dispose();
        _mutex = null;
    }

    private void StartActivationListener()
    {
        _listenerCts = new CancellationTokenSource();
        CancellationToken token = _listenerCts.Token;
        EventWaitHandle activation = _activationEvent!; // TryAcquire legt beide Ereignisse unmittelbar vor diesem Aufruf an
        EventWaitHandle quit = _quitEvent!;

        _listenerTask = Task.Run(() => Listen(activation, quit, token), token);
    }

    // Wartet, bis eine zweite Instanz aktiviert oder das Setup zum Beenden auffordert, und meldet
    // es weiter, bis der Listener beim Freigeben abgebrochen wird.
    private void Listen(EventWaitHandle activation, EventWaitHandle quit, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            int signaled = WaitHandle.WaitAny(
                new WaitHandle[] { activation, quit, token.WaitHandle },
                millisecondsTimeout: Timeout.Infinite);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (signaled == 0)
            {
                ActivationRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (signaled == 1)
            {
                QuitRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
