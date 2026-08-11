using LookAway.Core.Entities;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests für <see cref="WorkTask"/>: die Aufgabe, an der beim aufgabenbasierten
/// Pausenmodell gearbeitet wird.
/// </summary>
public sealed class WorkTaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_MakesAnOpenTask()
    {
        WorkTask task = WorkTask.Create("Angebot für Meier fertigstellen", Now);

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal("Angebot für Meier fertigstellen", task.Text);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Null(task.CompletedAt);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void Create_TrimsSurroundingSpace()
        => Assert.Equal("Bericht schreiben", WorkTask.Create("  Bericht schreiben  ", Now).Text);

    /// <remarks>
    /// <c>ThrowsAny</c>, weil <c>null</c> die speziellere <see cref="ArgumentNullException"/>
    /// auslöst — beides ist richtig, geprüft wird die Ablehnung.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_RejectsEmptyText(string? text)
        => Assert.ThrowsAny<ArgumentException>(() => WorkTask.Create(text!, Now));

    /// <remarks>
    /// Eine Kachel mit einem Absatz Text ist keine Aufgabe mehr, sondern eine Notiz —
    /// und sie sprengt die Liste, in der sie steht.
    /// </remarks>
    [Fact]
    public void Create_RejectsOverlyLongText()
        => Assert.Throws<ArgumentException>(
            () => WorkTask.Create(new string('x', WorkTask.MaxTextLength + 1), Now));

    [Fact]
    public void Create_AcceptsTextAtTheLimit()
        => Assert.Equal(
            WorkTask.MaxTextLength,
            WorkTask.Create(new string('x', WorkTask.MaxTextLength), Now).Text.Length);

    [Fact]
    public void Complete_MarksTheTaskDone()
    {
        WorkTask done = WorkTask.Create("Ablage aufräumen", Now).Complete(Now.AddHours(2));

        Assert.True(done.IsCompleted);
        Assert.Equal(Now.AddHours(2), done.CompletedAt);
    }

    /// <remarks>
    /// Die Aufgabe ist unveränderlich: Ein Abschluss liefert eine neue Fassung und
    /// lässt die alte in Ruhe. So kann keine Liste halb aktualisiert dastehen.
    /// </remarks>
    [Fact]
    public void Complete_LeavesTheOriginalUntouched()
    {
        WorkTask open = WorkTask.Create("Ablage aufräumen", Now);

        _ = open.Complete(Now.AddHours(2));

        Assert.False(open.IsCompleted);
    }

    [Fact]
    public void Complete_KeepsIdAndText()
    {
        WorkTask open = WorkTask.Create("Ablage aufräumen", Now);

        WorkTask done = open.Complete(Now.AddHours(2));

        Assert.Equal(open.Id, done.Id);
        Assert.Equal(open.Text, done.Text);
        Assert.Equal(open.CreatedAt, done.CreatedAt);
    }

    [Fact]
    public void Complete_RejectsAnEndBeforeTheStart()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => WorkTask.Create("Ablage aufräumen", Now).Complete(Now.AddMinutes(-1)));

    [Fact]
    public void Reopen_ClearsTheCompletion()
    {
        WorkTask again = WorkTask.Create("Ablage aufräumen", Now).Complete(Now.AddHours(2)).Reopen();

        Assert.False(again.IsCompleted);
        Assert.Null(again.CompletedAt);
    }

    [Fact]
    public void WithText_ChangesOnlyTheText()
    {
        WorkTask task = WorkTask.Create("Alter Text", Now).Complete(Now.AddHours(1));

        WorkTask renamed = task.WithText("Neuer Text");

        Assert.Equal("Neuer Text", renamed.Text);
        Assert.Equal(task.Id, renamed.Id);
        Assert.True(renamed.IsCompleted);
    }

    [Fact]
    public void WithText_RejectsEmptyText()
        => Assert.Throws<ArgumentException>(() => WorkTask.Create("Alter Text", Now).WithText("  "));
}
