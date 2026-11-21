using BenchmarkDotNet.Attributes;
using LookAway.Data.Logging;
using LookAway.MeasurementData;

namespace LookAway.Benchmarks;

/// <summary>
/// Was die Protokollseite beim Öffnen liest: die jüngsten Einträge aus einer Woche Logdateien.
/// </summary>
/// <remarks>
/// 500 ist die Grenze der Protokollseite (<c>LogViewModel.MaxEntries</c>); 2.000 zeigt, was eine
/// höhere Grenze kostete. 1.000 Zeilen am Tag sind ein ruhiger Tag, 5.000 ein lauter.
/// </remarks>
[MemoryDiagnoser]
public class LogBenchmarks : TemporaryDirectoryBenchmark
{
    private RollingFileLogReader? _reader;

    /// <summary>Wie viele Einträge die Seite anfordert.</summary>
    [Params(500, 2000)]
    public int Entries { get; set; }

    /// <summary>Einträge je Tagesdatei.</summary>
    [Params(1000, 5000)]
    public int LinesPerDay { get; set; }

    /// <summary>Legt eine Woche Protokoll an und prüft, dass die angeforderte Zahl zurückkommt.</summary>
    /// <returns>Der Vorgang.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        string directory = CreateDirectory("logs");
        _ = LogWeek.WriteTo(directory, new DateOnly(2026, 11, 21), LinesPerDay);
        _reader = new RollingFileLogReader(directory);

        int read = (await _reader.ReadRecentAsync(Entries).ConfigureAwait(false)).Count;
        if (read != Entries)
        {
            throw new InvalidOperationException($"Messbestand nicht gelesen: {read} von {Entries} Einträgen.");
        }
    }


    /// <summary>Liest die jüngsten Einträge.</summary>
    /// <returns>Zahl der gelesenen Einträge.</returns>
    [Benchmark]
    public async Task<int> ReadRecent() => (await _reader!.ReadRecentAsync(Entries).ConfigureAwait(false)).Count;
}
