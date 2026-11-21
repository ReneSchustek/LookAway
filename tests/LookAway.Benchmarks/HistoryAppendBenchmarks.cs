using BenchmarkDotNet.Attributes;
using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Data.Repositories;
using LookAway.MeasurementData;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Benchmarks;

/// <summary>
/// Was jedes Pausenende tut: eine Pause an die Jahres-Historie anhängen. Die Ablage liest dafür die
/// ganze Datei und schreibt sie neu.
/// </summary>
/// <remarks>
/// Einzeln je Durchgang und vor jedem Durchgang auf den Jahresbestand zurückgesetzt: Sonst wüchse die
/// Datei mit jeder Messung, und die letzten Werte gälten einer anderen Größe als die ersten.
/// </remarks>
[MemoryDiagnoser]
[InvocationCount(1)]
public class HistoryAppendBenchmarks : TemporaryDirectoryBenchmark
{
    private string _file = string.Empty;
    private string _pristine = string.Empty;
    private JsonBreakHistoryRepository? _repository;
    private BreakSession? _next;

    /// <summary>Legt den Jahresbestand und eine unveränderte Kopie an.</summary>
    [GlobalSetup]
    public void Setup()
    {
        string directory = CreateDirectory("append");
        _file = Path.Combine(directory, "history.json");
        _pristine = Path.Combine(directory, "history.pristine.json");
        HistoryYear.WriteTo(_pristine, HistoryYear.Create(HistoryBenchmarks.Now));
        _repository = new JsonBreakHistoryRepository(_file, NullLogger<JsonBreakHistoryRepository>.Instance);
        _next = new BreakSession(Guid.NewGuid(), HistoryBenchmarks.Now, HistoryBenchmarks.Now.AddMinutes(5), BreakModel.ClassicPomodoro, BreakOutcome.Taken);
    }

    /// <summary>Setzt die Historie vor jedem Durchgang auf den Jahresbestand zurück.</summary>
    [IterationSetup]
    public void RestoreYear() => File.Copy(_pristine, _file, overwrite: true);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repository?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Hängt eine Pause an die Jahres-Historie an.</summary>
    /// <returns>Der Vorgang.</returns>
    [Benchmark]
    public Task AppendOneBreak() => _repository!.AppendAsync(_next!);
}
