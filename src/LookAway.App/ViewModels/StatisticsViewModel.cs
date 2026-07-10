using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LookAway.Core.Entities;
using LookAway.Core.Interfaces;
using LookAway.Core.Localization;
using LookAway.Core.Services;
using LookAway.Core.ValueObjects;

namespace LookAway.App.ViewModels;

/// <summary>
/// Bereitet die Pausen-Statistik für die Anzeige auf und stößt den
/// CSV-Export an. UI-frei und damit testbar.
/// </summary>
internal sealed partial class StatisticsViewModel : ObservableObject
{
    private const double MaxBarHeight = 80.0;

    private readonly StatisticsService _statisticsService;
    private readonly IBreakHistoryRepository _historyRepository;
    private readonly CsvExporter _csvExporter;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    public partial int TodayCount { get; set; }

    [ObservableProperty]
    public partial int TodaySkipped { get; set; }

    [ObservableProperty]
    public partial string TodayBreakTime { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StatBarViewModel> WeekBars { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<StatBarViewModel> YearBars { get; set; }

    /// <summary>
    /// Erzeugt das ViewModel.
    /// </summary>
    /// <param name="statisticsService">Aggregiert die Historie.</param>
    /// <param name="historyRepository">Quelle aller Sitzungen (für den Export).</param>
    /// <param name="csvExporter">Erzeugt die CSV-Darstellung.</param>
    /// <param name="localization">Liefert die Beschriftungen.</param>
    public StatisticsViewModel(
        StatisticsService statisticsService,
        IBreakHistoryRepository historyRepository,
        CsvExporter csvExporter,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(statisticsService);
        ArgumentNullException.ThrowIfNull(historyRepository);
        ArgumentNullException.ThrowIfNull(csvExporter);
        ArgumentNullException.ThrowIfNull(localization);

        _statisticsService = statisticsService;
        _historyRepository = historyRepository;
        _csvExporter = csvExporter;
        _localization = localization;

        // Startwerte der gebundenen Eigenschaften; partielle Properties erlauben
        // keine Feld-Initialisierer.
        TodayBreakTime = string.Empty;
        WeekBars = [];
        YearBars = [];
    }

    /// <summary>Wird ausgelöst, wenn der CSV-Inhalt zum Speichern bereitsteht.</summary>
    public event EventHandler<CsvExportRequestedEventArgs>? CsvExportRequested;

    /// <summary>Aktualisiert alle gebundenen Beschriftungen (nach Sprachwechsel).</summary>
    public void RefreshTexts() => OnPropertyChanged(string.Empty);

    /// <summary>Tab-Überschrift "Statistik".</summary>
    public string TabStatisticsHeader => _localization.GetText(SettingsTextKeys.TabStatistics);

    /// <summary>Abschnitts-Überschrift "Heute".</summary>
    public string TodayHeader => _localization.GetText(SettingsTextKeys.StatisticsToday);

    /// <summary>Abschnitts-Überschrift "Diese Woche".</summary>
    public string WeekHeader => _localization.GetText(SettingsTextKeys.StatisticsWeek);

    /// <summary>Abschnitts-Überschrift "Dieses Jahr".</summary>
    public string YearHeader => _localization.GetText(SettingsTextKeys.StatisticsYear);

    /// <summary>Beschriftung "Pausen".</summary>
    public string BreaksLabel => _localization.GetText(SettingsTextKeys.StatisticsBreaks);

    /// <summary>Beschriftung "Pausenzeit".</summary>
    public string BreakTimeLabel => _localization.GetText(SettingsTextKeys.StatisticsBreakTime);

    /// <summary>Beschriftung "Übersprungen".</summary>
    public string SkippedLabel => _localization.GetText(SettingsTextKeys.StatisticsSkipped);

    /// <summary>Beschriftung des Export-Buttons.</summary>
    public string ExportLabel => _localization.GetText(SettingsTextKeys.StatisticsExport);

    /// <summary>
    /// Lädt die aktuellen Kennzahlen aus der Historie.
    /// </summary>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        PeriodStatistics today = await _statisticsService.GetTodayAsync(cancellationToken).ConfigureAwait(true);
        PeriodStatistics week = await _statisticsService.GetThisWeekAsync(cancellationToken).ConfigureAwait(true);
        PeriodStatistics year = await _statisticsService.GetThisYearAsync(cancellationToken).ConfigureAwait(true);

        TodayCount = today.Count;
        TodaySkipped = today.Skipped;
        TodayBreakTime = FormatDuration(today.TotalBreakTime);
        WeekBars = ToBars(week.Buckets);
        YearBars = ToBars(year.Buckets);
    }

    [RelayCommand]
    private async Task ExportCsvAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<BreakSession> sessions = await _historyRepository.LoadAllAsync(cancellationToken).ConfigureAwait(true);
        string csv = _csvExporter.Export(sessions);
        CsvExportRequested?.Invoke(this, new CsvExportRequestedEventArgs(csv));
    }

    private static List<StatBarViewModel> ToBars(IReadOnlyList<StatBucket> buckets)
    {
        int max = buckets.Count == 0 ? 0 : buckets.Max(bucket => bucket.Count);
        List<StatBarViewModel> bars = new(buckets.Count);
        foreach (StatBucket bucket in buckets)
        {
            double height = max == 0 ? 0 : bucket.Count / (double)max * MaxBarHeight;
            bars.Add(new StatBarViewModel(bucket.Label, bucket.Count, height));
        }

        return bars;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        int totalMinutes = (int)duration.TotalMinutes;
        return totalMinutes >= 60
            ? string.Create(CultureInfo.CurrentCulture, $"{totalMinutes / 60} h {totalMinutes % 60} min")
            : string.Create(CultureInfo.CurrentCulture, $"{totalMinutes} min");
    }
}
