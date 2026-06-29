using System.Globalization;
using LookAway.Core.Enums;

namespace LookAway.Application.ViewModels;

/// <summary>
/// Zustand und Countdown-Logik des Pausen-Overlays — UI-frei und damit ohne
/// WinUI testbar. Die View (App-Schicht) dunkelt den Bildschirm ab, bindet an
/// dieses ViewModel, ruft im Sekundentakt <see cref="Tick"/> auf und meldet
/// einen ESC-Druck ueber <see cref="EndByUser"/>.
/// </summary>
/// <remarks>
/// Das erste Ende gewinnt: ein abgelaufener Countdown und ein gleichzeitiger
/// ESC-Druck koennen sich nicht gegenseitig ueberschreiben, und ein spaeter
/// <see cref="Tick"/> feuert kein zweites <see cref="Ended"/>.
/// </remarks>
public sealed class BreakOverlayViewModel
{
    /// <summary>Lokalisierungs-Schluessel des Overlay-Titels.</summary>
    public const string TitleKey = "Overlay.Title";

    /// <summary>Lokalisierungs-Schluessel des ESC-Hinweises.</summary>
    public const string EndHintKey = "Overlay.EndHint";

    private bool _ended;

    /// <summary>
    /// Erzeugt das ViewModel fuer eine Pause.
    /// </summary>
    /// <param name="hintKey">Lokalisierungs-Schluessel des Uebungs-Hinweises.</param>
    /// <param name="breakDuration">Dauer der Pause (positiv).</param>
    public BreakOverlayViewModel(string hintKey, TimeSpan breakDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hintKey);
        if (breakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(breakDuration), breakDuration, "Pausendauer muss positiv sein.");
        }

        HintKey = hintKey;
        // Auf volle Sekunden aufrunden, damit die Anzeige nicht sofort eine
        // Sekunde zu niedrig startet.
        RemainingSeconds = (int)Math.Ceiling(breakDuration.TotalSeconds);
    }

    /// <summary>Wird ausgeloest, sobald die Pause endet (genau einmal).</summary>
    public event EventHandler<BreakOverlayEndedEventArgs>? Ended;

    /// <summary>Lokalisierungs-Schluessel des angezeigten Uebungs-Hinweises.</summary>
    public string HintKey { get; }

    /// <summary>Verbleibende Pausensekunden.</summary>
    public int RemainingSeconds { get; private set; }

    /// <summary>Wahr, sobald die Pause beendet wurde.</summary>
    public bool IsEnded => _ended;

    /// <summary>Verbleibende Zeit als <c>m:ss</c> fuer die Anzeige.</summary>
    public string RemainingDisplay
    {
        get
        {
            TimeSpan remaining = TimeSpan.FromSeconds(RemainingSeconds);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)remaining.TotalMinutes}:{remaining.Seconds:00}");
        }
    }

    /// <summary>
    /// Zaehlt den Countdown um eine Sekunde herunter. Erreicht er null, endet
    /// die Pause mit <see cref="BreakEndReason.Elapsed"/>.
    /// </summary>
    public void Tick()
    {
        if (_ended)
        {
            return;
        }

        RemainingSeconds--;
        if (RemainingSeconds <= 0)
        {
            RemainingSeconds = 0;
            Complete(BreakEndReason.Elapsed);
        }
    }

    /// <summary>Beendet die Pause auf Benutzerwunsch (ESC).</summary>
    public void EndByUser() => Complete(BreakEndReason.EndedByUser);

    private void Complete(BreakEndReason reason)
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        Ended?.Invoke(this, new BreakOverlayEndedEventArgs(reason));
    }
}
