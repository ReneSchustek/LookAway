using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.Application.Localization;

/// <summary>
/// Sprachneutrale Schlüssel der Hotkey-Modifikatornamen und ein Helfer, der eine
/// <see cref="HotkeyDefinition"/> mit lokalisierten Modifikatornamen rendert.
/// </summary>
public static class HotkeyTextKeys
{
    /// <summary>Modifikator <c>Strg</c>/<c>Ctrl</c>.</summary>
    public const string ModifierControl = "Hotkey.Modifier.Control";

    /// <summary>Modifikator <c>Alt</c>.</summary>
    public const string ModifierAlt = "Hotkey.Modifier.Alt";

    /// <summary>Modifikator <c>Umschalt</c>/<c>Shift</c>/<c>Maj</c>.</summary>
    public const string ModifierShift = "Hotkey.Modifier.Shift";

    /// <summary>Modifikator <c>Win</c>.</summary>
    public const string ModifierWin = "Hotkey.Modifier.Win";

    /// <summary>
    /// Liefert die lesbare Tastenkombination mit Modifikatornamen in der aktuellen
    /// Sprache, z. B. <c>"Strg+Alt+P"</c> bzw. <c>"Ctrl+Alt+P"</c>.
    /// </summary>
    /// <param name="definition">Die anzuzeigende Tastenkombination.</param>
    /// <param name="localization">Quelle der lokalisierten Modifikatornamen.</param>
    /// <returns>Die lokalisierte Darstellung.</returns>
    public static string Format(HotkeyDefinition definition, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return definition.Format(modifier => localization.GetText(KeyFor(modifier)));
    }

    private static string KeyFor(HotkeyModifiers modifier) => modifier switch
    {
        HotkeyModifiers.Control => ModifierControl,
        HotkeyModifiers.Alt => ModifierAlt,
        HotkeyModifiers.Shift => ModifierShift,
        HotkeyModifiers.Win => ModifierWin,
        _ => modifier.ToString(),
    };
}
