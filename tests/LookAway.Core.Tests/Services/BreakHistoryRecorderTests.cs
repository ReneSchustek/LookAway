using LookAway.Core.Entities;
using LookAway.Core.Enums;
using LookAway.Core.Services;
using LookAway.Core.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Core.Tests;

/// <summary>
/// Das Schreiben in die Pausen-Historie: was im Eintrag steht und dass ein Schreibfehler den
/// Pausen-Ablauf nicht aufhält.
/// </summary>
public sealed class BreakHistoryRecorderTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Record_StoresModelOutcomeAndTimes()
    {
        FakeBreakHistoryRepository history = new();
        BreakHistoryRecorder recorder = new(history, NullLogger<BreakHistoryRecorder>.Instance);

        recorder.Record(BreakModel.Ultradian, BreakOutcome.Taken, Start, Start.AddMinutes(20));

        BreakSession session = Assert.Single(history.Sessions);
        Assert.Equal(BreakModel.Ultradian, session.Model);
        Assert.Equal(BreakOutcome.Taken, session.Outcome);
        Assert.Equal(Start, session.StartedAt);
        Assert.Equal(Start.AddMinutes(20), session.EndedAt);
        Assert.Null(session.TaskId);
    }

    /// <remarks>Die Statistik bleibt dann eben unverändert; die Pause selbst läuft weiter.</remarks>
    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Record_WhenTheHistoryCannotBeWritten_DoesNotThrow(Type failure)
    {
        FakeBreakHistoryRepository history = new() { AppendFailure = (Exception)Activator.CreateInstance(failure)! }; // beide Typen haben einen parameterlosen Konstruktor
        BreakHistoryRecorder recorder = new(history, NullLogger<BreakHistoryRecorder>.Instance);

        Exception? thrown = Record.Exception(() => recorder.Record(BreakModel.Ultradian, BreakOutcome.Skipped, Start, Start));

        Assert.Null(thrown);
        Assert.Empty(history.Sessions);
    }
}
