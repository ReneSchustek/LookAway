using LookAway.Core.Entities;
using LookAway.Core.Enums;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für die Invarianten der <see cref="BreakSession"/>-Entität.
/// </summary>
public sealed class BreakSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_SetsTheValuesAndComputesTheDuration()
    {
        BreakSession session = new(
            Guid.NewGuid(),
            Start,
            Start.AddMinutes(5),
            BreakModel.ClassicPomodoro,
            BreakOutcome.Taken);

        Assert.Equal(TimeSpan.FromMinutes(5), session.Duration);
        Assert.Equal(BreakOutcome.Taken, session.Outcome);
    }

    [Fact]
    public void EmptyId_IsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new BreakSession(Guid.Empty, Start, Start, BreakModel.ClassicPomodoro, BreakOutcome.Taken));
    }

    [Fact]
    public void EndBeforeStart_IsRejected()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BreakSession(Guid.NewGuid(), Start, Start.AddMinutes(-1), BreakModel.ClassicPomodoro, BreakOutcome.Taken));
    }

    /// <remarks>
    /// Der Verlauf wird aus einer Datei gelesen, die auch von Hand geändert sein kann.
    /// Ein unbekannter Zahlenwert darf dort nicht als gültiges Modell durchrutschen.
    /// </remarks>
    [Fact]
    public void UndefinedModel_IsRejected()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BreakSession(Guid.NewGuid(), Start, Start.AddMinutes(5), (BreakModel)99, BreakOutcome.Taken));
    }

    [Fact]
    public void UndefinedOutcome_IsRejected()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BreakSession(Guid.NewGuid(), Start, Start.AddMinutes(5), BreakModel.ClassicPomodoro, (BreakOutcome)99));
    }
}
