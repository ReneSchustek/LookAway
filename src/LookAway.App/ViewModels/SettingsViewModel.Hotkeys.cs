using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Domain;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;

namespace LookAway.App.ViewModels;

/// <summary>
/// Hotkey-Belange des <see cref="SettingsViewModel"/>: Belegung der drei globalen
/// Aktionen, ihre lokalisierten Beschriftungen/Anzeigetexte und das Zurücksetzen
/// auf die Standardbelegung. Laden und Speichern liegen im Kern-ViewModel.
/// </summary>
internal sealed partial class SettingsViewModel
{
    private HotkeyDefinition _hotkeyStartBreak = HotkeyDefaults.StartBreak;
    private HotkeyDefinition _hotkeySkipOrSnooze = HotkeyDefaults.SkipOrSnooze;
    private HotkeyDefinition _hotkeyToggleDnd = HotkeyDefaults.ToggleDnd;

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

    [RelayCommand]
    private void ResetHotkeys()
    {
        _hotkeyStartBreak = HotkeyDefaults.StartBreak;
        _hotkeySkipOrSnooze = HotkeyDefaults.SkipOrSnooze;
        _hotkeyToggleDnd = HotkeyDefaults.ToggleDnd;
        OnPropertyChanged(nameof(HotkeyStartBreakText));
        OnPropertyChanged(nameof(HotkeySkipOrSnoozeText));
        OnPropertyChanged(nameof(HotkeyToggleDndText));
    }
}
