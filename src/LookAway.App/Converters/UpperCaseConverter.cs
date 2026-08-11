using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace LookAway.App.Converters;

/// <summary>
/// Schreibt einen Text in Großbuchstaben.
/// </summary>
/// <remarks>
/// Abschnittsbeschriftungen stehen in der Gestaltungslinie klein, gesperrt und in
/// Großbuchstaben. Die Umwandlung geschieht bei der Anzeige und nicht in den
/// Sprachdateien: Dort behalten die Texte ihre normale Schreibweise und bleiben für
/// andere Stellen — etwa die Menüpunkte — brauchbar.
/// </remarks>
internal sealed partial class UpperCaseConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string text ? text.ToUpper(CultureFor(language)) : value;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Die Umwandlung ist einseitig.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("Großschreibung lässt sich nicht zurücknehmen.");

    // Die Sprache entscheidet mit: Ein türkisches „i" wird anders groß geschrieben
    // als ein deutsches. Ohne Angabe gilt die Kultur des Benutzers.
    private static CultureInfo CultureFor(string language)
        => string.IsNullOrEmpty(language)
            ? CultureInfo.CurrentCulture
            : new CultureInfo(language);
}
