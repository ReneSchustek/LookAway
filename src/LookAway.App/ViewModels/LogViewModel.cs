using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.App.ViewModels;

/// <summary>
/// Das Anwendungsprotokoll als durchsuchbare, filterbare Liste — UI-frei und damit
/// ohne WinUI testbar.
/// </summary>
/// <remarks>
/// Bis hierher lag das Protokoll nur als Datei im Datenverzeichnis: Wer nach einem
/// fehlgeschlagenen Update oder einem stummen Hotkey nachsehen wollte, musste den
/// Ordner erst finden. Gezeigt werden die jüngsten Einträge, nicht der ganze
/// Bestand — mehr braucht niemand, um zu sehen, was gerade schiefgegangen ist.
/// </remarks>
internal sealed partial class LogViewModel : ObservableObject
{
    /// <summary>
    /// Obergrenze der geladenen Einträge. Sieben Tage Aufbewahrung können an einem
    /// schlechten Tag viele Zeilen ergeben; die Ansicht bleibt trotzdem flüssig.
    /// </summary>
    private const int MaxEntries = 500;

    private readonly ILogEntryReader _reader;
    private readonly ILocalizationService _localization;
    private readonly IClock _clock;
    private readonly List<LogListItem> _allEntries = [];

    /// <summary>Erzeugt das ViewModel.</summary>
    /// <param name="reader">Quelle der Protokolleinträge.</param>
    /// <param name="localization">Liefert Beschriftungen und Stufen-Wörter.</param>
    /// <param name="clock">Zeitquelle für den Zeitraum-Filter.</param>
    public LogViewModel(ILogEntryReader reader, ILocalizationService localization, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(clock);

        _reader = reader;
        _localization = localization;
        _clock = clock;

        // Reihenfolge zählt: Das Setzen von SearchText löst den Filterlauf aus, und
        // der greift auf die Sammlung zu.
        VisibleEntries = [];
        SearchText = string.Empty;
    }

    /// <summary>Die nach Suche und Filtern sichtbaren Einträge, neueste zuerst.</summary>
    public ObservableCollection<LogListItem> VisibleEntries { get; }

    /// <summary>Der eingegebene Suchtext; wirkt beim Tippen.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; }

    /// <summary>Der gewählte Stufen-Filter.</summary>
    [ObservableProperty]
    public partial LogLevelFilter LevelFilter { get; set; }

    /// <summary>Der gewählte Zeitraum-Filter.</summary>
    [ObservableProperty]
    public partial LogPeriodFilter PeriodFilter { get; set; }

    /// <summary>
    /// Wahr, wenn überhaupt nichts protokolliert ist.
    /// </summary>
    /// <remarks>
    /// Von <see cref="ShowNoResults"/> zu trennen: Ein leeres Protokoll ist eine
    /// andere Lage als eine Suche ohne Treffer, und ein „Suche zurücksetzen" hilft
    /// dort nicht weiter.
    /// </remarks>
    public bool ShowEmpty => _allEntries.Count == 0;

    /// <summary>Wahr, wenn Suche oder Filter alles ausgeblendet haben.</summary>
    public bool ShowNoResults => _allEntries.Count > 0 && VisibleEntries.Count == 0;

    /// <summary>Wahr, solange Einträge zu sehen sind.</summary>
    public bool HasResults => VisibleEntries.Count > 0;

    /// <summary>Erklärende Zeile unter dem Titel.</summary>
    public string Subtitle => _localization.GetText(SettingsTextKeys.LogSubtitle);

    /// <summary>Platzhalter des Suchfelds; er benennt, was durchsucht wird.</summary>
    public string SearchPlaceholder => _localization.GetText(SettingsTextKeys.LogSearchPlaceholder);

    /// <summary>Beschriftung des Stufen-Chips "Alle".</summary>
    public string LevelAllLabel => _localization.GetText(SettingsTextKeys.LogLevelAll);

    /// <summary>Beschriftung des Stufen-Chips "Hinweise".</summary>
    public string LevelInformationLabel => _localization.GetText(SettingsTextKeys.LogLevelInformation);

    /// <summary>Beschriftung des Stufen-Chips "Warnungen".</summary>
    public string LevelWarningLabel => _localization.GetText(SettingsTextKeys.LogLevelWarning);

    /// <summary>Beschriftung des Stufen-Chips "Fehler".</summary>
    public string LevelErrorLabel => _localization.GetText(SettingsTextKeys.LogLevelError);

    /// <summary>Beschriftung des Zeitraum-Chips "Gesamter Zeitraum".</summary>
    public string PeriodAllLabel => _localization.GetText(SettingsTextKeys.LogPeriodAll);

    /// <summary>Beschriftung des Zeitraum-Chips "Heute".</summary>
    public string PeriodTodayLabel => _localization.GetText(SettingsTextKeys.LogPeriodToday);

    /// <summary>Beschriftung des Zeitraum-Chips "Letzte 7 Tage".</summary>
    public string PeriodWeekLabel => _localization.GetText(SettingsTextKeys.LogPeriodWeek);

    /// <summary>Überschrift des Leerzustands "noch nichts protokolliert".</summary>
    public string EmptyTitle => _localization.GetText(SettingsTextKeys.LogEmptyTitle);

    /// <summary>Erklärung des Leerzustands "noch nichts protokolliert".</summary>
    public string EmptyText => _localization.GetText(SettingsTextKeys.LogEmptyText);

    /// <summary>Überschrift des Leerzustands "nichts gefunden".</summary>
    public string NoResultsTitle => _localization.GetText(SettingsTextKeys.NoResultsTitle);

    /// <summary>Erklärung des Leerzustands "nichts gefunden".</summary>
    public string NoResultsText => _localization.GetText(SettingsTextKeys.NoResultsText);

    /// <summary>Beschriftung der Schaltfläche "Suche zurücksetzen".</summary>
    public string ResetSearchLabel => _localization.GetText(SettingsTextKeys.ResetSearch);

    /// <summary>Beschriftung der Schaltfläche "Neu laden".</summary>
    public string ReloadLabel => _localization.GetText(SettingsTextKeys.LogReload);

    /// <summary>Bindung des Stufen-Chips "Alle".</summary>
    public bool IsLevelAll
    {
        get => LevelFilter == LogLevelFilter.All;
        set => ToggleLevel(LogLevelFilter.All, value, nameof(IsLevelAll));
    }

    /// <summary>Bindung des Stufen-Chips "Hinweise".</summary>
    public bool IsLevelInformation
    {
        get => LevelFilter == LogLevelFilter.Information;
        set => ToggleLevel(LogLevelFilter.Information, value, nameof(IsLevelInformation));
    }

    /// <summary>Bindung des Stufen-Chips "Warnungen".</summary>
    public bool IsLevelWarning
    {
        get => LevelFilter == LogLevelFilter.Warning;
        set => ToggleLevel(LogLevelFilter.Warning, value, nameof(IsLevelWarning));
    }

    /// <summary>Bindung des Stufen-Chips "Fehler".</summary>
    public bool IsLevelError
    {
        get => LevelFilter == LogLevelFilter.Error;
        set => ToggleLevel(LogLevelFilter.Error, value, nameof(IsLevelError));
    }

    /// <summary>Bindung des Zeitraum-Chips "Gesamter Zeitraum".</summary>
    public bool IsPeriodAll
    {
        get => PeriodFilter == LogPeriodFilter.All;
        set => TogglePeriod(LogPeriodFilter.All, value, nameof(IsPeriodAll));
    }

    /// <summary>Bindung des Zeitraum-Chips "Heute".</summary>
    public bool IsPeriodToday
    {
        get => PeriodFilter == LogPeriodFilter.Today;
        set => TogglePeriod(LogPeriodFilter.Today, value, nameof(IsPeriodToday));
    }

    /// <summary>Bindung des Zeitraum-Chips "Letzte 7 Tage".</summary>
    public bool IsPeriodWeek
    {
        get => PeriodFilter == LogPeriodFilter.Week;
        set => TogglePeriod(LogPeriodFilter.Week, value, nameof(IsPeriodWeek));
    }

    /// <summary>
    /// Lädt die jüngsten Protokolleinträge. Suche und Filter bleiben dabei stehen —
    /// wer aktualisiert, will nicht seine Eingabe verlieren.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LogEntry> entries = await _reader
            .ReadRecentAsync(MaxEntries, cancellationToken)
            .ConfigureAwait(true);

        _allEntries.Clear();
        _allEntries.AddRange(entries.Select(ToListItem));

        ApplyFilter();
    }

    /// <summary>Aktualisiert die Texte nach einem Sprachwechsel.</summary>
    public void RefreshTexts() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private Task ReloadAsync() => LoadAsync();

    [RelayCommand]
    private void ResetSearch()
    {
        SearchText = string.Empty;
        LevelFilter = LogLevelFilter.All;
        PeriodFilter = LogPeriodFilter.All;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnLevelFilterChanged(LogLevelFilter value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsLevelAll));
        OnPropertyChanged(nameof(IsLevelInformation));
        OnPropertyChanged(nameof(IsLevelWarning));
        OnPropertyChanged(nameof(IsLevelError));
    }

    partial void OnPeriodFilterChanged(LogPeriodFilter value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsPeriodAll));
        OnPropertyChanged(nameof(IsPeriodToday));
        OnPropertyChanged(nameof(IsPeriodWeek));
    }

    // Je Leiste ist immer genau ein Chip gewählt. Klickt jemand den aktiven erneut an,
    // meldet die Bindung "abgewählt" — das wird zurückgesetzt, sonst stünde die Leiste
    // ohne Auswahl da und der Zustand der Liste wäre nicht mehr abzulesen.
    private void ToggleLevel(LogLevelFilter target, bool isChecked, string propertyName)
    {
        if (isChecked)
        {
            LevelFilter = target;
            return;
        }

        OnPropertyChanged(propertyName);
    }

    private void TogglePeriod(LogPeriodFilter target, bool isChecked, string propertyName)
    {
        if (isChecked)
        {
            PeriodFilter = target;
            return;
        }

        OnPropertyChanged(propertyName);
    }

    private void ApplyFilter()
    {
        string needle = SearchText.Trim();
        DateTimeOffset? from = PeriodStart();

        VisibleEntries.Clear();
        foreach (LogListItem item in _allEntries.Where(item => Matches(item, needle, from)))
        {
            VisibleEntries.Add(item);
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private bool Matches(LogListItem item, string needle, DateTimeOffset? from)
    {
        bool matchesLevel = LevelFilter switch
        {
            LogLevelFilter.Information => item.Level < LogLevel.Warning,
            LogLevelFilter.Warning => item.Level == LogLevel.Warning,
            LogLevelFilter.Error => item.Level >= LogLevel.Error,
            _ => true,
        };

        return matchesLevel
            && (from is null || item.Timestamp >= from)
            && (needle.Length == 0
                || item.Message.Contains(needle, StringComparison.CurrentCultureIgnoreCase));
    }

    // Untergrenze des gewählten Zeitraums; null bedeutet "ohne Untergrenze".
    // Gerechnet wird in Ortszeit, weil "heute" das ist, was auf der Uhr des
    // Benutzers steht — nicht der UTC-Tag.
    private DateTimeOffset? PeriodStart()
    {
        DateTimeOffset now = _clock.UtcNow.ToLocalTime();

        return PeriodFilter switch
        {
            LogPeriodFilter.Today => new DateTimeOffset(now.Date, now.Offset),
            LogPeriodFilter.Week => new DateTimeOffset(now.Date, now.Offset).AddDays(-6),
            _ => null,
        };
    }

    private LogListItem ToListItem(LogEntry entry) => new(
        entry.Timestamp,
        entry.Timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
        entry.Level,
        LevelText(entry.Level),
        entry.Category,
        entry.Message);

    // Die Stufe steht als Wort neben dem Eintrag: Ein Zustand, den nur die Farbe
    // trägt, ist für einen Teil der Benutzer gar nicht da.
    private string LevelText(LogLevel level) => level switch
    {
        >= LogLevel.Error => _localization.GetText(SettingsTextKeys.LogEntryError),
        LogLevel.Warning => _localization.GetText(SettingsTextKeys.LogEntryWarning),
        _ => _localization.GetText(SettingsTextKeys.LogEntryInformation),
    };
}
