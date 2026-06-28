using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Interfaces;

/// <summary>
/// Registriert systemweite Tastenkombinationen und meldet ihre Ausloesung.
/// Implementierungen kapseln die Plattform-Anbindung (Win32).
/// </summary>
public interface IHotkeyService
{
    /// <summary>Wird ausgeloest, wenn ein registrierter Hotkey gedrueckt wurde.</summary>
    event EventHandler<HotkeyPressedEventArgs> HotkeyPressed;

    /// <summary>
    /// Registriert die angegebenen Hotkeys neu. Zuvor registrierte werden zuerst
    /// freigegeben. Fehlgeschlagene Einzelregistrierungen (z. B. Konflikt mit einer
    /// anderen App) werden uebersprungen, ohne die uebrigen zu verhindern.
    /// </summary>
    /// <param name="bindings">Zuordnung Aktion → Tastenkombination.</param>
    void Register(IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> bindings);

    /// <summary>Gibt alle registrierten Hotkeys frei.</summary>
    void UnregisterAll();
}
