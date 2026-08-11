using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Domain;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;

namespace LookAway.App.ViewModels;

/// <summary>
/// Die Pausenmodelle als durchsuchbare, filterbare Liste — UI-frei und damit ohne
/// WinUI testbar.
/// </summary>
/// <remarks>
/// Sieben feste Modelle sind wenig für ein Suchfeld. Es steht trotzdem da, weil die
/// Anordnung aus Titel, Suche und Filterleiste in jeder Listen-Ansicht dieselbe ist:
/// Wer sie hier weglässt, nimmt genau die Wiedererkennung weg, um die es geht.
/// Die Kachel zeigt zusätzlich, was aus dem Modell entstanden ist — ohne diesen
/// Zusammenhang wäre die Liste nur eine Auswahlliste mit mehr Platzbedarf.
/// </remarks>
internal sealed partial class BreakModelListViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;
    private readonly IBreakHistoryRepository _history;
    private readonly List<BreakModelListItem> _allModels = [];

    /// <summary>Erzeugt das ViewModel.</summary>
    /// <param name="localization">Liefert die Anzeigenamen und Textvorlagen.</param>
    /// <param name="history">Quelle für die Zahl der aufgezeichneten Pausen je Modell.</param>
    public BreakModelListViewModel(ILocalizationService localization, IBreakHistoryRepository history)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(history);

        _localization = localization;
        _history = history;

        // Reihenfolge zählt: Das Setzen von SearchText löst den Filterlauf aus, und
        // der greift auf die Sammlung zu.
        VisibleModels = [];
        SearchText = string.Empty;
    }

    /// <summary>Wird ausgelöst, wenn der Benutzer eine Kachel wählt.</summary>
    public event EventHandler<BreakModel>? ModelSelected;

    /// <summary>Die nach Suche und Filter sichtbaren Modelle.</summary>
    public ObservableCollection<BreakModelListItem> VisibleModels { get; }

    /// <summary>Der eingegebene Suchtext; wirkt beim Tippen.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; }

    /// <summary>Der gewählte Filter.</summary>
    [ObservableProperty]
    public partial BreakModelFilter Filter { get; set; }

    /// <summary>Wahr, solange die gefilterte Liste Einträge hat.</summary>
    public bool HasResults => VisibleModels.Count > 0;

    /// <summary>
    /// Wahr, wenn Suche oder Filter alles ausgeblendet haben.
    /// </summary>
    /// <remarks>
    /// Die Gegenlage — „nichts vorhanden" — gibt es hier nicht: Die Modelle sind fest
    /// eingebaut. Ein Leerzustand kann also nur von der Eingabe kommen, und genau das
    /// sagt der Text; sonst sucht der Benutzer den Fehler in seinen Daten.
    /// </remarks>
    public bool ShowNoResults => VisibleModels.Count == 0;

    /// <summary>Beschriftung "Alle" der Filterleiste.</summary>
    public string FilterAllLabel => _localization.GetText(SettingsTextKeys.ModelFilterAll);

    /// <summary>Beschriftung "In Verwendung" der Filterleiste.</summary>
    public string FilterActiveLabel => _localization.GetText(SettingsTextKeys.ModelFilterActive);

    /// <summary>Beschriftung "Übrige" der Filterleiste.</summary>
    public string FilterInactiveLabel => _localization.GetText(SettingsTextKeys.ModelFilterInactive);

    /// <summary>Platzhalter des Suchfelds; er benennt, was durchsucht wird.</summary>
    public string SearchPlaceholder => _localization.GetText(SettingsTextKeys.ModelSearchPlaceholder);

    /// <summary>Erklärende Zeile unter dem Titel.</summary>
    public string Subtitle => _localization.GetText(SettingsTextKeys.ModelSubtitle);

    /// <summary>Überschrift des Leerzustands "nichts gefunden".</summary>
    public string NoResultsTitle => _localization.GetText(SettingsTextKeys.NoResultsTitle);

    /// <summary>Erklärung des Leerzustands "nichts gefunden".</summary>
    public string NoResultsText => _localization.GetText(SettingsTextKeys.NoResultsText);

    /// <summary>Beschriftung der Schaltfläche "Suche zurücksetzen".</summary>
    public string ResetSearchLabel => _localization.GetText(SettingsTextKeys.ResetSearch);

    /// <summary>Kennzeichnung der gerade verwendeten Kachel.</summary>
    public string ActiveBadgeLabel => _localization.GetText(SettingsTextKeys.ModelActiveBadge);

    /// <summary>Bindung des Chips "Alle".</summary>
    public bool IsFilterAll
    {
        get => Filter == BreakModelFilter.All;
        set => ToggleFilter(BreakModelFilter.All, value, nameof(IsFilterAll));
    }

    /// <summary>Bindung des Chips "In Verwendung".</summary>
    public bool IsFilterActive
    {
        get => Filter == BreakModelFilter.Active;
        set => ToggleFilter(BreakModelFilter.Active, value, nameof(IsFilterActive));
    }

    /// <summary>Bindung des Chips "Übrige".</summary>
    public bool IsFilterInactive
    {
        get => Filter == BreakModelFilter.Inactive;
        set => ToggleFilter(BreakModelFilter.Inactive, value, nameof(IsFilterInactive));
    }

    /// <summary>
    /// Baut die Liste auf und zählt die aufgezeichneten Pausen je Modell.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<BreakModel, int> counts = await CountBreaksPerModelAsync(cancellationToken)
            .ConfigureAwait(true);

        BreakModel active = ActiveModel;
        _allModels.Clear();

        foreach (BreakModel model in Enum.GetValues<BreakModel>())
        {
            _ = counts.TryGetValue(model, out int count);
            _allModels.Add(new BreakModelListItem(
                model,
                _localization.GetText(SettingsTextKeys.ForModel(model)),
                _localization.GetText(BreakModelRegistry.GetHintKey(model)),
                count,
                FormatBreakCount(count))
            {
                IsActive = model == active,
            });
        }

        ApplyFilter();
    }

    /// <summary>
    /// Setzt die Marke auf das Modell, das gerade verwendet wird.
    /// </summary>
    /// <param name="model">Das verwendete Modell.</param>
    public void SetActiveModel(BreakModel model)
    {
        ActiveModel = model;

        foreach (BreakModelListItem item in _allModels)
        {
            item.IsActive = item.Model == model;
        }

        // Steht der Filter auf "In Verwendung", zeigt er nach dem Wechsel sonst
        // weiter das alte Modell.
        ApplyFilter();
    }

    /// <summary>Aktualisiert die Texte nach einem Sprachwechsel.</summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public Task RefreshTextsAsync(CancellationToken cancellationToken = default)
    {
        OnPropertyChanged(string.Empty);
        return LoadAsync(cancellationToken);
    }

    private BreakModel ActiveModel { get; set; } = BreakModel.ClassicPomodoro;

    [RelayCommand]
    private void Select(BreakModelListItem? item)
    {
        if (item is null)
        {
            return;
        }

        SetActiveModel(item.Model);
        ModelSelected?.Invoke(this, item.Model);
    }

    [RelayCommand]
    private void ResetSearch()
    {
        SearchText = string.Empty;
        Filter = BreakModelFilter.All;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(BreakModelFilter value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(IsFilterInactive));
    }

    // Ein Filter ist immer gewählt. Klickt jemand den aktiven Chip erneut an, meldet
    // die Bindung "abgewählt" — das wird nicht übernommen, sondern zurückgesetzt.
    // Sonst stünde die Leiste ohne Auswahl da, und der Zustand der Liste wäre nicht
    // mehr abzulesen.
    private void ToggleFilter(BreakModelFilter target, bool isChecked, string propertyName)
    {
        if (isChecked)
        {
            Filter = target;
            return;
        }

        OnPropertyChanged(propertyName);
    }

    private void ApplyFilter()
    {
        string needle = SearchText.Trim();

        VisibleModels.Clear();
        foreach (BreakModelListItem item in _allModels.Where(item => Matches(item, needle)))
        {
            VisibleModels.Add(item);
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private bool Matches(BreakModelListItem item, string needle)
    {
        bool matchesFilter = Filter switch
        {
            BreakModelFilter.Active => item.IsActive,
            BreakModelFilter.Inactive => !item.IsActive,
            _ => true,
        };

        return matchesFilter
            && (needle.Length == 0
                || item.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase));
    }

    private async Task<IReadOnlyDictionary<BreakModel, int>> CountBreaksPerModelAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BreakSession> sessions = await _history.LoadAllAsync(cancellationToken).ConfigureAwait(true);

        return sessions
            .GroupBy(session => session.Model)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private string FormatBreakCount(int count) => string.Format(
        CultureInfo.CurrentCulture,
        _localization.GetText(SettingsTextKeys.ModelBreakCount),
        count);
}
