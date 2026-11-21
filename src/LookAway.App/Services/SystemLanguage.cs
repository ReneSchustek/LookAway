using System.Globalization;
using LookAway.Core.Enums;

namespace LookAway.App.Services;

/// <summary>Leitet die Sprache für den ersten Start aus der Windows-Anzeigesprache ab.</summary>
internal static class SystemLanguage
{
    /// <summary>
    /// Deutsch und Französisch werden übernommen, jede andere Anzeigesprache führt zu Englisch.
    /// </summary>
    /// <param name="culture">Anzeigesprache, etwa <see cref="CultureInfo.CurrentUICulture"/>.</param>
    /// <returns>Die vorgeschlagene Sprache.</returns>
    public static Language FromCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return culture.TwoLetterISOLanguageName switch
        {
            "de" => Language.German,
            "fr" => Language.French,
            _ => Language.English,
        };
    }
}
