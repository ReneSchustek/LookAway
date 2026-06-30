using System.Globalization;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Stellt sicher, dass pro Windows-Benutzer nur eine LookAway-Instanz
/// aktiv ist. Verwendet einen benannten <see cref="Mutex"/> im
/// <c>Local\</c>-Namespace und einen <see cref="EventWaitHandle"/> als
/// minimaler IPC, damit eine Zweit-Instanz die laufende Instanz benachrichtigen
/// kann (z. B. um das Settings-Fenster anzuzeigen).
/// </summary>
public sealed class SingleInstanceLock : ISingleInstanceLock
{
    /// <summary>Wird gefeuert, wenn eine Zweit-Instanz signalisiert hat.</summary>
    public event EventHandler? ActivationRequested;

    private readonly string _mutexName;
    private readonly string _eventName;
    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
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
            _ = _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Fehler des Listener-Tasks beim Herunterfahren werden bewusst toleriert.
        }

        _listenerCts?.Dispose();
        _activationEvent?.Dispose();

        if (_mutex is not null)
        {
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
    }

    private void StartActivationListener()
    {
        _listenerCts = new CancellationTokenSource();
        CancellationToken token = _listenerCts.Token;
        EventWaitHandle handle = _activationEvent!;

        _listenerTask = Task.Run(
            () =>
            {
                while (!token.IsCancellationRequested)
                {
                    int signaled = WaitHandle.WaitAny(
                        new WaitHandle[] { handle, token.WaitHandle },
                        millisecondsTimeout: Timeout.Infinite);

                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (signaled == 0)
                    {
                        ActivationRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
            },
            token);
    }
}
