using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;

namespace LookAway.App.ViewModels;

/// <summary>
/// Hotkey-Belange des <see cref="SettingsViewModel"/>: Belegung der drei globalen
/// Aktionen, ihre lokalisierten Beschriftungen/Anzeigetexte, das Aufnehmen einer
/// eigenen Tastenkombination und das Zurücksetzen auf die Standardbelegung.
/// Laden und Speichern liegen im Kern-ViewModel.
/// </summary>
internal sealed partial class SettingsViewModel
{
    private HotkeyDefinition _hotkeyStartBreak = HotkeyDefaults.StartBreak;
    private HotkeyDefinition _hotkeySkipOrSnooze = HotkeyDefaults.SkipOrSnooze;
    private HotkeyDefinition _hotkeyToggleDnd = HotkeyDefaults.ToggleDnd;

    // Läuft gerade eine Aufnahme, steht hier die Aktion, die neu belegt wird.
    // Die Oberfläche liest daran ab, welche Zeile auf einen Tastendruck wartet.
    private HotkeyAction? _capturingAction;

    /// <summary>
    /// Meldet Beginn (<c>true</c>) und Ende (<c>false</c>) einer Aufnahme.
    /// <para>
    /// Hintergrund: Solange die eigenen Hotkeys systemweit registriert sind, fängt
    /// Windows genau diese Kombinationen ab — sie erreichen das Fenster nie und
    /// lösen stattdessen die Aktion aus. Wer die Registrierung hält, muss sie
    /// deshalb während der Aufnahme lösen. Das Ereignis sagt nur Bescheid; die
    /// Registrierung selbst bleibt außerhalb, damit diese Schicht ohne
    /// Plattform-Anbindung prüfbar bleibt.
    /// </para>
    /// </summary>
    public event EventHandler<bool>? HotkeyCaptureChanged;

    /// <summary>Tab-Überschrift "Hotkeys".</summary>
    public string TabHotkeysHeader => _localization.GetText(SettingsTextKeys.TabHotkeys);

    /// <summary>Beschriftung der Hotkey-aktivieren-Option.</summary>
    public string HotkeysEnableLabel => _localization.GetText(SettingsTextKeys.HotkeysEnableLabel);

    /// <summary>Beschriftung der Aktion "Pause starten".</summary>
    public string HotkeyStartBreakLabel => _localization.GetText(SettingsTextKeys.HotkeyStartBreak);

    /// <summary>Beschriftung der Aktion "Überspringen/Snooze".</summary>
    public string HotkeySkipOrSnoozeLabel => _localization.GetText(SettingsTextKeys.HotkeySkipOrSnooze);

    /// <summary>Beschriftung der Aktion "DND umschalten".</summary>
    public string HotkeyToggleDndLabel => _localization.GetText(SettingsTextKeys.HotkeyToggleDnd);

    /// <summary>Beschriftung des Zurücksetzen-Buttons.</summary>
    public string HotkeysResetLabel => _localization.GetText(SettingsTextKeys.HotkeysReset);

    /// <summary>Anzeigetext des "Pause starten"-Hotkeys (lokalisiert).</summary>
    public string HotkeyStartBreakText => HotkeyTextKeys.Format(_hotkeyStartBreak, _localization);

    /// <summary>Anzeigetext des "Überspringen/Snooze"-Hotkeys (lokalisiert).</summary>
    public string HotkeySkipOrSnoozeText => HotkeyTextKeys.Format(_hotkeySkipOrSnooze, _localization);

    /// <summary>Anzeigetext des "DND umschalten"-Hotkeys (lokalisiert).</summary>
    public string HotkeyToggleDndText => HotkeyTextKeys.Format(_hotkeyToggleDnd, _localization);

    /// <summary>Beschriftung der Schaltfläche, die eine Neubelegung startet.</summary>
    public string HotkeysCaptureLabel => _localization.GetText(SettingsTextKeys.HotkeysCapture);

    /// <summary>Wahr, solange auf eine Tastenkombination gewartet wird.</summary>
    public bool IsCapturingHotkey => _capturingAction is not null;

    /// <summary>
    /// Rückmeldung zur letzten Aufnahme: Aufforderung während der Aufnahme, danach
    /// Erfolg, Ablehnung wegen fehlendem Modifikator oder Hinweis auf die Kollision.
    /// Leer, solange nichts aufgenommen wurde.
    /// </summary>
    public string HotkeyCaptureHint { get; private set; } = string.Empty;

    /// <summary>
    /// Startet die Aufnahme für eine Aktion. Ein zweiter Aufruf verwirft eine noch
    /// laufende Aufnahme und beginnt neu — es kann immer nur eine Zeile warten.
    /// </summary>
    /// <param name="action">Aktion, die neu belegt werden soll.</param>
    [RelayCommand]
    private void BeginHotkeyCapture(HotkeyAction action)
    {
        bool warSchonAktiv = _capturingAction is not null;
        _capturingAction = action;
        SetCaptureHint(SettingsTextKeys.HotkeysCapturePrompt);

        if (!warSchonAktiv)
        {
            HotkeyCaptureChanged?.Invoke(this, true);
        }
    }

    /// <summary>Bricht eine laufende Aufnahme ab, ohne die Belegung zu ändern.</summary>
    [RelayCommand]
    private void CancelHotkeyCapture()
    {
        if (_capturingAction is null)
        {
            return;
        }

        _capturingAction = null;
        SetCaptureHint(SettingsTextKeys.HotkeysCaptureCancelled);
        HotkeyCaptureChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Übernimmt die aufgenommene Kombination, sofern eine Aufnahme läuft und die
    /// Kombination gültig und frei ist. Die Oberfläche liefert nur den Tastendruck;
    /// die Entscheidung fällt hier, damit sie ohne Fenster prüfbar bleibt.
    /// </summary>
    /// <param name="candidate">Aufgenommene Kombination.</param>
    /// <returns><c>true</c>, wenn die Belegung übernommen wurde.</returns>
    public bool TryCompleteHotkeyCapture(HotkeyDefinition candidate)
    {
        if (_capturingAction is not HotkeyAction action)
        {
            return false;
        }

        if (!HotkeyValidator.IsValid(candidate))
        {
            SetCaptureHint(SettingsTextKeys.HotkeysCaptureInvalid);
            return false;
        }

        // Gegen die *anderen* Aktionen prüfen: Dieselbe Kombination erneut auf
        // dieselbe Aktion zu legen ist keine Kollision, sondern ein No-op.
        if (IsTakenByOtherAction(action, candidate))
        {
            SetCaptureHint(SettingsTextKeys.HotkeysCaptureTaken);
            return false;
        }

        Assign(action, candidate);
        _capturingAction = null;
        SetCaptureHint(SettingsTextKeys.HotkeysCaptureAssigned);
        HotkeyCaptureChanged?.Invoke(this, false);
        return true;
    }

    [RelayCommand]
    private void ResetHotkeys()
    {
        bool aufnahmeLief = _capturingAction is not null;
        _capturingAction = null;
        _hotkeyStartBreak = HotkeyDefaults.StartBreak;
        _hotkeySkipOrSnooze = HotkeyDefaults.SkipOrSnooze;
        _hotkeyToggleDnd = HotkeyDefaults.ToggleDnd;
        HotkeyCaptureHint = string.Empty;
        NotifyHotkeysChanged();

        if (aufnahmeLief)
        {
            HotkeyCaptureChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Beendet eine noch laufende Aufnahme. Wird beim Schließen des Fensters
    /// gerufen: Ein Aufnahmezustand, der niemand mehr sieht, dürfte sonst die
    /// Hotkeys dauerhaft abgemeldet lassen.
    /// </summary>
    private void EndCaptureOnDispose()
    {
        if (_capturingAction is null)
        {
            return;
        }

        _capturingAction = null;
        HotkeyCaptureChanged?.Invoke(this, false);
    }

    private bool IsTakenByOtherAction(HotkeyAction action, HotkeyDefinition candidate)
    {
        Dictionary<HotkeyAction, HotkeyDefinition> bindings = new()
        {
            [HotkeyAction.StartBreak] = _hotkeyStartBreak,
            [HotkeyAction.SkipOrSnooze] = _hotkeySkipOrSnooze,
            [HotkeyAction.ToggleDnd] = _hotkeyToggleDnd,
        };
        bindings[action] = candidate;

        return HotkeyValidator.FindConflicts(bindings).Contains(action);
    }

    private void Assign(HotkeyAction action, HotkeyDefinition definition)
    {
        switch (action)
        {
            case HotkeyAction.StartBreak:
                _hotkeyStartBreak = definition;
                break;
            case HotkeyAction.SkipOrSnooze:
                _hotkeySkipOrSnooze = definition;
                break;
            case HotkeyAction.ToggleDnd:
                _hotkeyToggleDnd = definition;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unbekannte Hotkey-Aktion.");
        }

        NotifyHotkeysChanged();
    }

    private void SetCaptureHint(string textKey)
    {
        HotkeyCaptureHint = _localization.GetText(textKey);
        OnPropertyChanged(nameof(HotkeyCaptureHint));
        OnPropertyChanged(nameof(IsCapturingHotkey));
    }

    private void NotifyHotkeysChanged()
    {
        OnPropertyChanged(nameof(HotkeyStartBreakText));
        OnPropertyChanged(nameof(HotkeySkipOrSnoozeText));
        OnPropertyChanged(nameof(HotkeyToggleDndText));
    }
}
