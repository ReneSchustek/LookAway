using LookAway.Core.Domain;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für <see cref="LogEntryParser"/>: das Gegenstück zum Schreibformat des
/// Datei-Protokolls, inklusive der Fortsetzungszeilen eines Ausnahme-Stapels.
/// </summary>
public sealed class LogEntryParserTests
{
    private const string Header = "[2026-08-11T12:34:56.789Z] [Information] LookAway.Core.Services.TimerService: Timer gestartet";

    [Fact]
    public void Parse_ReadsTimestampLevelCategoryAndMessage()
    {
        LogEntry entry = Assert.Single(LogEntryParser.Parse([Header]));

        Assert.Equal(new DateTimeOffset(2026, 8, 11, 12, 34, 56, 789, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("LookAway.Core.Services.TimerService", entry.Category);
        Assert.Equal("Timer gestartet", entry.Message);
    }

    [Fact]
    public void Parse_AppendsContinuationLinesToPrecedingEntry()
    {
        // So sieht ein protokollierter Ausnahme-Stapel aus: Kopfzeile, dann Zeilen ohne Kopf.
        string[] lines =
        [
            "[2026-08-11T12:00:00.000Z] [Error] LookAway.App: Fehler beim Speichern",
            "System.IO.IOException: Zugriff verweigert",
            "   bei LookAway.Data.AtomicFile.Write()",
        ];

        LogEntry entry = Assert.Single(LogEntryParser.Parse(lines));

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("System.IO.IOException", entry.Message, StringComparison.Ordinal);
        Assert.Contains("AtomicFile.Write", entry.Message, StringComparison.Ordinal);
        Assert.StartsWith("Fehler beim Speichern", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_KeepsEntriesInReadOrder()
    {
        string[] lines =
        [
            "[2026-08-11T08:00:00.000Z] [Information] A: erster",
            "[2026-08-11T09:00:00.000Z] [Warning] B: zweiter",
        ];

        IReadOnlyList<LogEntry> entries = LogEntryParser.Parse(lines);

        Assert.Equal(2, entries.Count);
        Assert.Equal("erster", entries[0].Message);
        Assert.Equal("zweiter", entries[1].Message);
        Assert.Equal(LogLevel.Warning, entries[1].Level);
    }

    /// <remarks>
    /// Eine abgeschnittene Datei kann mit einer Stapelzeile beginnen. Ohne Kopf hat sie
    /// weder Zeit noch Stufe — als eigener Eintrag wäre sie eine Zeile ohne Aussage.
    /// </remarks>
    [Fact]
    public void Parse_DropsFragmentsBeforeTheFirstHeader()
    {
        string[] lines =
        [
            "   bei LookAway.Data.AtomicFile.Write()",
            Header,
        ];

        LogEntry entry = Assert.Single(LogEntryParser.Parse(lines));

        Assert.Equal("Timer gestartet", entry.Message);
    }

    [Fact]
    public void Parse_KeepsColonsInsideTheMessage()
    {
        string[] lines = ["[2026-08-11T12:00:00.000Z] [Information] A: Ziel: C:\\Programme\\LookAway"];

        LogEntry entry = Assert.Single(LogEntryParser.Parse(lines));

        Assert.Equal("A", entry.Category);
        Assert.Equal("Ziel: C:\\Programme\\LookAway", entry.Message);
    }

    [Theory]
    [InlineData("kein Kopf, nur Text")]
    [InlineData("[keine Zeit] [Information] A: Meldung")]
    [InlineData("[2026-08-11T12:00:00.000Z] [Unbekannt] A: Meldung")]
    [InlineData("")]
    public void Parse_IgnoresLinesWithoutAValidHeader(string line)
        => Assert.Empty(LogEntryParser.Parse([line]));

    [Fact]
    public void Parse_ReturnsEmptyListForEmptyInput()
        => Assert.Empty(LogEntryParser.Parse([]));

    [Fact]
    public void Parse_ThrowsWhenLinesAreNull()
        => Assert.Throws<ArgumentNullException>(() => LogEntryParser.Parse(null!));
}
