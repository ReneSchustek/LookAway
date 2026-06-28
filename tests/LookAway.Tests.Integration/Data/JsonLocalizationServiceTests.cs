using LookAway.Core.Enums;
using LookAway.Data.Services;

namespace LookAway.Tests.Integration.Data;

/// <summary>
/// Tests fuer den <see cref="JsonLocalizationService"/> gegen die echten,
/// eingebetteten Sprachtabellen.
/// </summary>
public sealed class JsonLocalizationServiceTests
{
    [Fact]
    public void GetText_liefert_den_deutschen_Text_fuer_einen_bekannten_Schluessel()
    {
        JsonLocalizationService service = new(Language.German);

        string text = service.GetText("Settings.Title");

        Assert.Equal("Einstellungen", text);
    }

    [Fact]
    public void GetText_faellt_bei_unbefuellter_Sprache_auf_Deutsch_zurueck()
    {
        // Englisch ist noch nicht befuellt und faellt auf Deutsch zurueck.
        JsonLocalizationService service = new(Language.English);

        string text = service.GetText("Settings.Title");

        Assert.Equal("Einstellungen", text);
    }

    [Fact]
    public void GetText_liefert_den_Schluessel_wenn_er_nirgends_existiert()
    {
        JsonLocalizationService service = new(Language.German);

        string text = service.GetText("Nicht.Vorhanden.Schluessel");

        Assert.Equal("Nicht.Vorhanden.Schluessel", text);
    }

    [Fact]
    public void SetLanguage_loest_LanguageChanged_nur_bei_echter_Aenderung_aus()
    {
        JsonLocalizationService service = new(Language.German);
        int raised = 0;
        service.LanguageChanged += (_, _) => raised++;

        service.SetLanguage(Language.German); // keine Aenderung
        service.SetLanguage(Language.French); // Aenderung

        Assert.Equal(1, raised);
        Assert.Equal(Language.French, service.CurrentLanguage);
    }
}
