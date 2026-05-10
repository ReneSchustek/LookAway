using LookAway.Application.Services;

namespace LookAway.Tests.Unit.Application.Services;

/// <summary>
/// Tests fuer den <see cref="SingleInstanceLock"/>. Da der Mutex
/// prozesslokal pro <c>Local\</c>-Namespace agiert, koennen sich zwei
/// Lock-Instanzen im selben Prozess gegenseitig sehen — das nutzen wir,
/// um die Acquire-/Signal-Logik deterministisch zu pruefen.
/// </summary>
public sealed class SingleInstanceLockTests
{
    [Fact]
    public void TryAcquire_OnFreshLock_ReturnsTrue()
    {
        string user = UniqueUser();
        using SingleInstanceLock first = new(user);

        bool acquired = first.TryAcquire();

        Assert.True(acquired);
        Assert.True(first.IsOwner);
    }

    [Fact]
    public void TryAcquire_OnSecondInstance_ReturnsFalse()
    {
        string user = UniqueUser();
        using SingleInstanceLock first = new(user);
        Assert.True(first.TryAcquire());

        using SingleInstanceLock second = new(user);
        bool acquired = second.TryAcquire();

        Assert.False(acquired);
        Assert.False(second.IsOwner);
    }

    [Fact]
    public void Dispose_ReleasesLockSoNextInstanceCanAcquire()
    {
        string user = UniqueUser();
        SingleInstanceLock first = new(user);
        Assert.True(first.TryAcquire());
        first.Dispose();

        using SingleInstanceLock second = new(user);
        Assert.True(second.TryAcquire());
    }

    [Fact]
    public async Task SignalExistingInstance_FromSecond_TriggersActivationOnFirst()
    {
        string user = UniqueUser();
        using SingleInstanceLock first = new(user);
        Assert.True(first.TryAcquire());

        using ManualResetEventSlim received = new(initialState: false);
        first.ActivationRequested += (_, _) => received.Set();

        using SingleInstanceLock second = new(user);
        Assert.False(second.TryAcquire());

        bool signaled = second.SignalExistingInstance();
        Assert.True(signaled);

        bool eventFired = await Task.Run(() => received.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(eventFired);
    }

    [Fact]
    public void SignalExistingInstance_WithoutFirstInstance_ReturnsFalse()
    {
        string user = UniqueUser();
        using SingleInstanceLock orphan = new(user);

        bool signaled = orphan.SignalExistingInstance();

        Assert.False(signaled);
    }

    [Fact]
    public void Constructor_RejectsBlankUserName()
    {
        _ = Assert.Throws<ArgumentException>(() => new SingleInstanceLock("  "));
    }

    [Fact]
    public void TryAcquire_AfterDispose_Throws()
    {
        string user = UniqueUser();
        SingleInstanceLock lockHandle = new(user);
        lockHandle.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => lockHandle.TryAcquire());
    }

    /// <summary>
    /// Liefert einen eindeutigen Benutzernamen pro Test, damit parallele
    /// Testlaeufe sich nicht ueber den globalen Mutex-Namespace beeinflussen.
    /// </summary>
    private static string UniqueUser() => "ITTest-" + Guid.NewGuid().ToString("N");
}
