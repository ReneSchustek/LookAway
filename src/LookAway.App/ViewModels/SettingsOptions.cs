using LookAway.Core.Interfaces;

namespace LookAway.App.ViewModels;

/// <summary>Baut Auswahllisten über Aufzählungen, beschriftet in der aktuellen Sprache.</summary>
internal static class SettingsOptions
{
    /// <summary>Eine Auswahl je Wert der Aufzählung, beschriftet über ihren Textschlüssel.</summary>
    /// <typeparam name="T">Die Aufzählung, etwa Sprache oder Pausenmodell.</typeparam>
    /// <param name="localization">Liefert die Beschriftung; ein Sprachwechsel frischt sie auf.</param>
    /// <param name="textKey">Textschlüssel zu einem Wert.</param>
    /// <returns>Die Auswahlliste in der Reihenfolge der Aufzählung.</returns>
    public static List<SettingsOption<T>> For<T>(ILocalizationService localization, Func<T, string> textKey)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(textKey);

        return Enum.GetValues<T>()
            .Select(value => new SettingsOption<T>(value, () => localization.GetText(textKey(value))))
            .ToList();
    }
}
