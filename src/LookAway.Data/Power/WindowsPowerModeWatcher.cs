using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using LookAway.Core.Interfaces;
using Microsoft.Win32;

namespace LookAway.Data.Power;

/// <summary>
/// Bindet sich an <see cref="SystemEvents.PowerModeChanged"/> und
/// übersetzt Suspend/Resume in plattformneutrale
/// <see cref="IPowerModeWatcher"/>-Events.
/// </summary>
/// <remarks>
/// Von der Abdeckungsmessung ausgenommen: Ausgelöst wird hier nichts vom eigenen
/// Code, sondern vom Ruhezustand des Rechners. Was die Klasse tut, zeigt sich beim
/// Zuklappen des Deckels, nicht im Test.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Reine Systemanbindung ohne eigene Fachlogik.")]
[SupportedOSPlatform("windows")]
public sealed class WindowsPowerModeWatcher : IPowerModeWatcher
{
    private readonly Lock _lock = new();
    private bool _started;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler? Suspending;

    /// <inheritdoc />
    public event EventHandler? Resuming;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_started)
            {
                return;
            }

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _started = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_started)
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                _started = false;
            }
            _disposed = true;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                Suspending?.Invoke(this, EventArgs.Empty);
                break;
            case PowerModes.Resume:
                Resuming?.Invoke(this, EventArgs.Empty);
                break;
            case PowerModes.StatusChange:
                // AC/Battery-Wechsel ignorieren — kein Sleep/Resume.
                break;
            default:
                break;
        }
    }
}
