using LookAway.Core.Enums;
using LookAway.Core.Localization;

namespace LookAway.App.ViewModels;

/// <summary>
/// Sprache und Auswahllisten: die Sprachwahl mit sofortiger Vorschau, die Auswahllisten aller
/// Seiten und das Auffrischen ihrer Beschriftungen nach einem Sprachwechsel.
/// </summary>
internal sealed partial class SettingsViewModel
{
    /// <summary>Auswählbare Sprachen mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<Language>> Languages { get; private set; } = [];

    /// <summary>Auswählbare Pausenmodelle mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<BreakModel>> Models { get; private set; } = [];

    /// <summary>Auswählbare Erscheinungsbilder mit lokalisierter Beschriftung.</summary>
    public IReadOnlyList<SettingsOption<AppTheme>> Appearances { get; private set; } = [];

    /// <summary>Wählt die Sprache anhand ihres Werts (UI/Test-Hilfe).</summary>
    /// <param name="language">Zu wählende Sprache.</param>
    public void SelectLanguage(Language language)
        => SelectedLanguageOption = Languages.First(option => option.Value == language);

    partial void OnSelectedLanguageOptionChanged(SettingsOption<Language>? value)
    {
        if (_isInitializing || value is null)
        {
            return;
        }

        _localization.SetLanguage(value.Value);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshLabels(Languages);
        RefreshLabels(Models);
        RefreshLabels(Sounds);
        RefreshLabels(UpdateFrequencies);
        RefreshLabels(Appearances);
        RefreshLabels(OverlayBackgrounds);

        // Fehlertexte in neuer Sprache, danach alle gebundenen Texte aktualisieren.
        Validate();
        Texts.Refresh();
        Statistics.RefreshTexts();
        Log.RefreshTexts();

        // Die Modell-Kacheln tragen übersetzte Namen und Sätze; sie werden neu
        // aufgebaut, statt nur die Bindungen anzustoßen. Die Aufgaben ebenso.
        _ = BreakModels.RefreshTextsAsync();
        _ = Tasks.RefreshTextsAsync();

        OnPropertyChanged(string.Empty);
    }

    private static void RefreshLabels<T>(IEnumerable<SettingsOption<T>> options)
    {
        foreach (SettingsOption<T> option in options)
        {
            option.RefreshLabel();
        }
    }

    // Eine Auswahlliste je Einstellung mit festem Wertebereich; die Beschriftungen folgen der Sprache.
    private void BuildAllOptions()
    {
        Languages = SettingsOptions.For<Language>(_localization, SettingsTextKeys.ForLanguage);
        Models = SettingsOptions.For<BreakModel>(_localization, SettingsTextKeys.ForModel);
        Sounds = SettingsOptions.For<SoundType>(_localization, SettingsTextKeys.ForSound);
        Appearances = SettingsOptions.For<AppTheme>(_localization, SettingsTextKeys.ForTheme);
        UpdateFrequencies = SettingsOptions.For<UpdateCheckFrequency>(_localization, SettingsTextKeys.ForFrequency);
        OverlayBackgrounds = SettingsOptions.For<OverlayBackground>(_localization, SettingsTextKeys.ForOverlayBackground);
    }
}
