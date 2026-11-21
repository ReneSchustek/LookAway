using System.Globalization;
using LookAway.App.Services;
using LookAway.Core.Enums;

namespace LookAway.App.Tests;

/// <summary>Vorschlag der Sprache beim ersten Start aus der Windows-Anzeigesprache.</summary>
public sealed class SystemLanguageTests
{
    [Theory]
    [InlineData("de-DE", Language.German)]
    [InlineData("de-AT", Language.German)]
    [InlineData("fr-FR", Language.French)]
    [InlineData("en-US", Language.English)]
    [InlineData("es-ES", Language.English)]
    public void Culture_MapsToASupportedLanguage(string culture, Language expected)
        => Assert.Equal(expected, SystemLanguage.FromCulture(CultureInfo.GetCultureInfo(culture)));
}
