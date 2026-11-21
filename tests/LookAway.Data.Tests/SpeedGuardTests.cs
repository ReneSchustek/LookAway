using System.Diagnostics;
using LookAway.Core.Services;
using LookAway.Core.ValueObjects;
using LookAway.Data.Logging;
using LookAway.Data.Repositories;
using LookAway.Data.Services;
using LookAway.MeasurementData;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Data.Tests;

/// <summary>
/// Grobe Wächter über die Wege, die mit dem Bestand wachsen: Historie und Statistik über ein Jahr,
/// eine laute Woche Protokoll, ein großes Foto vor der Pause.
/// </summary>
/// <remarks>
/// Das sind keine Messungen — die stehen im Messprojekt <c>tests/LookAway.Benchmarks</c>. Die
/// Grenzen liegen ein Vielfaches über den gemessenen Werten, damit kein langsamer Rechner und keine
/// Schwankung sie reißt. Schlagen sie an, ist etwas grundsätzlich schlechter geworden.
/// </remarks>
public sealed class SpeedGuardTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 17, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lookaway-speed-guards", TestIds.Next().ToString("N"));

    public SpeedGuardTests() => _ = Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task HistoryYear_LoadsAppendsAndFeedsTheStatisticsPage()
    {
        string file = Path.Combine(_directory, "history.json");
        IReadOnlyList<Core.Entities.BreakSession> year = HistoryYear.Create(Now);
        HistoryYear.WriteTo(file, year);
        using JsonBreakHistoryRepository repository = new(file, NullLogger<JsonBreakHistoryRepository>.Instance);
        StatisticsService statistics = new(repository, new FixedTestClock(Now));
        Assert.Equal(year.Count, (await repository.LoadAllAsync(TestContext.Current.CancellationToken)).Count); // auch: vor dem Messen einmal warm

        TimeSpan statisticsPage = await TimeAsync(async () =>
        {
            _ = await statistics.GetTodayAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            _ = await statistics.GetThisWeekAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.True((await statistics.GetThisYearAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).Count > 0);
        });
        TimeSpan append = await TimeAsync(() => repository.AppendAsync(year[0], TestContext.Current.CancellationToken));

        Assert.True(statisticsPage < GuardLimits.StatisticsPage, $"Statistikseite über ein Jahr: {statisticsPage.TotalMilliseconds:F0} ms");
        Assert.True(append < GuardLimits.AppendOneBreak, $"Pause an ein Jahr anhängen: {append.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public async Task LoudLogWeek_ReadsThePageQuickly()
    {
        _ = LogWeek.WriteTo(_directory, new DateOnly(2026, 11, 21), linesPerDay: 5000);
        RollingFileLogReader reader = new(_directory);
        Assert.Equal(500, (await reader.ReadRecentAsync(500, TestContext.Current.CancellationToken)).Count);

        TimeSpan read = await TimeAsync(() => reader.ReadRecentAsync(500, TestContext.Current.CancellationToken));

        Assert.True(read < GuardLimits.LogPage, $"Protokollseite über eine laute Woche: {read.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public async Task TwelveMegapixelPhoto_IsReadBeforeTheBreakQuickly()
    {
        string photo = Path.Combine(_directory, "foto.jpg");
        (int width, int height) = PhotoFile.TwelveMegapixels;
        await PhotoFile.WriteJpegAsync(photo, width, height);
        WindowsOverlayImageInspector inspector = new(NullLogger<WindowsOverlayImageInspector>.Instance);
        OverlayImageInfo? first = await inspector.InspectAsync(photo, TestContext.Current.CancellationToken);
        Assert.Equal(width, first?.PixelWidth);

        TimeSpan inspect = await TimeAsync(() => inspector.InspectAsync(photo, TestContext.Current.CancellationToken));

        Assert.True(inspect < GuardLimits.PhotoBeforeBreak, $"12-Megapixel-Foto prüfen: {inspect.TotalMilliseconds:F0} ms");
    }

    private static async Task<TimeSpan> TimeAsync(Func<Task> action)
    {
        Stopwatch watch = Stopwatch.StartNew();
        await action().ConfigureAwait(false);
        return watch.Elapsed;
    }

    /// <summary>
    /// Die Grenzen der Wächter, je mit dem gemessenen Wert, von dem sie abgeleitet sind (Messprojekt,
    /// Ryzen 7 5800X, 21.11.2026). Rund das Zwanzig- bis Fünfzigfache: Ein langsamer Rechner oder ein
    /// beschäftigter CI-Läufer reißt sie nicht, eine Verschlechterung um Größenordnungen schon.
    /// </summary>
    private static class GuardLimits
    {
        /// <summary>Gemessen 41 ms.</summary>
        public static readonly TimeSpan StatisticsPage = TimeSpan.FromSeconds(2);

        /// <summary>Gemessen 26 ms.</summary>
        public static readonly TimeSpan AppendOneBreak = TimeSpan.FromSeconds(1);

        /// <summary>Gemessen 11 ms.</summary>
        public static readonly TimeSpan LogPage = TimeSpan.FromSeconds(2);

        /// <summary>Gemessen 123 ms.</summary>
        public static readonly TimeSpan PhotoBeforeBreak = TimeSpan.FromSeconds(3);
    }

    /// <summary>Uhr mit festem Zeitpunkt, passend zum Ende des Messbestands.</summary>
    private sealed class FixedTestClock(DateTimeOffset now) : Core.Interfaces.IClock
    {
        public DateTimeOffset UtcNow => now;

        public TimeSpan MonotonicElapsed => TimeSpan.Zero;
    }
}
