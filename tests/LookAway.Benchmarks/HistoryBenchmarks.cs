using BenchmarkDotNet.Attributes;
using LookAway.Core.Entities;
using LookAway.Core.Services;
using LookAway.Data.Repositories;
using LookAway.MeasurementData;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Benchmarks;

/// <summary>
/// Die Historie über ein Jahr: laden, wie es Statistik und Export tun, die Statistikseite und der
/// CSV-Export über den vollen Bestand.
/// </summary>
[MemoryDiagnoser]
public class HistoryBenchmarks : TemporaryDirectoryBenchmark
{
    /// <summary>Ende des gemessenen Jahres.</summary>
    internal static readonly DateTimeOffset Now = new(2026, 11, 21, 17, 0, 0, TimeSpan.Zero);

    private readonly CsvExporter _exporter = new();
    private IReadOnlyList<BreakSession> _sessions = [];
    private JsonBreakHistoryRepository? _repository;
    private StatisticsService? _statistics;

    /// <summary>Legt den Jahresbestand an und prüft, dass ihn die echte Ablage vollständig einliest.</summary>
    /// <returns>Der Vorgang.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        string file = Path.Combine(CreateDirectory("history"), "history.json");
        _sessions = HistoryYear.Create(Now);
        HistoryYear.WriteTo(file, _sessions);

        _repository = new JsonBreakHistoryRepository(file, NullLogger<JsonBreakHistoryRepository>.Instance);
        _statistics = new StatisticsService(_repository, new FixedClock(Now));

        int loaded = (await _repository.LoadAllAsync().ConfigureAwait(false)).Count;
        if (loaded != _sessions.Count)
        {
            throw new InvalidOperationException($"Messbestand nicht eingelesen: {loaded} von {_sessions.Count} Pausen.");
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repository?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Liest die ganze Historie, wie es jede Auswertung tut.</summary>
    /// <returns>Zahl der gelesenen Pausen.</returns>
    [Benchmark]
    public async Task<int> LoadYear() => (await _repository!.LoadAllAsync().ConfigureAwait(false)).Count;

    /// <summary>Was die Statistikseite beim Öffnen tut: heute, diese Woche, dieses Jahr.</summary>
    /// <returns>Zahl der Pausen im Jahr.</returns>
    [Benchmark]
    public async Task<int> StatisticsPage()
    {
        _ = await _statistics!.GetTodayAsync().ConfigureAwait(false);
        _ = await _statistics.GetThisWeekAsync().ConfigureAwait(false);
        return (await _statistics.GetThisYearAsync().ConfigureAwait(false)).Count;
    }

    /// <summary>Der CSV-Export über das ganze Jahr.</summary>
    /// <returns>Länge des Exports in Zeichen.</returns>
    [Benchmark]
    public int CsvExportYear() => _exporter.Export(_sessions).Length;
}
