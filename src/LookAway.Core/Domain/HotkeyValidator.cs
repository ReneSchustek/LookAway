using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;

namespace LookAway.Core.Domain;

/// <summary>
/// Prueft globale Hotkeys auf Gueltigkeit und Kollisionen (BRIEF019). Reine,
/// plattformunabhaengige Logik — ohne echte Registrierung testbar.
/// </summary>
public static class HotkeyValidator
{
    private const HotkeyModifiers RequiredAnyOf =
        HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win;

    /// <summary>
    /// Eine Tastenkombination ist gueltig, wenn sie eine Taste und mindestens
    /// einen "echten" Modifikator (Strg, Alt oder Win) hat. Umschalt allein reicht
    /// nicht, da solche Kombinationen mit normaler Eingabe kollidieren.
    /// </summary>
    /// <param name="definition">Zu pruefende Kombination.</param>
    /// <returns><c>true</c>, wenn gueltig.</returns>
    public static bool IsValid(HotkeyDefinition definition)
        => definition.HasKey && (definition.Modifiers & RequiredAnyOf) != HotkeyModifiers.None;

    /// <summary>
    /// Ermittelt Aktionen, die sich dieselbe (gueltige) Kombination teilen.
    /// </summary>
    /// <param name="bindings">Zu pruefende Zuordnung.</param>
    /// <returns>Aktionen, die in einer Kollision stehen.</returns>
    public static IReadOnlyCollection<HotkeyAction> FindConflicts(
        IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        HashSet<HotkeyAction> conflicting = new();
        List<KeyValuePair<HotkeyAction, HotkeyDefinition>> entries = bindings.ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                if (entries[i].Value == entries[j].Value)
                {
                    _ = conflicting.Add(entries[i].Key);
                    _ = conflicting.Add(entries[j].Key);
                }
            }
        }

        return conflicting;
    }
}
