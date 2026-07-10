using LookAway.Core.Services;
using LookAway.Core.Entities;
using LookAway.Core.Exceptions;
using LookAway.Core.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Core.Tests;

/// <summary>
/// Tests der "Settings-Logik" des <see cref="AutoStartCoordinator"/> mit
/// <see cref="FakeAutoStartService"/> und <see cref="InMemorySettingsRepository"/> —
/// ohne echte Registry. Geprüft werden beide Synchronisations-Richtungen:
/// Benutzeränderung -> Registry und Startup-Abgleich Registry -> Einstellungen.
/// </summary>
public sealed class AutoStartCoordinatorTests
{
    [Fact]
    public async Task SetEnabledAsync_WhenEnabling_WritesRegistryAndPersistsSetting()
    {
        FakeAutoStartService autoStart = new();
        InMemorySettingsRepository repository = new();
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        await coordinator.SetEnabledAsync(true);

        Assert.Equal(1, autoStart.EnableCallCount);
        Assert.True(repository.PersistedAutoStart);
        Assert.Equal(1, repository.SaveCallCount);
    }

    [Fact]
    public async Task SetEnabledAsync_WhenDisabling_RemovesRegistryAndPersistsSetting()
    {
        FakeAutoStartService autoStart = new(initiallyEnabled: true);
        InMemorySettingsRepository repository = new(new Settings { AutoStart = true });
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        await coordinator.SetEnabledAsync(false);

        Assert.Equal(1, autoStart.DisableCallCount);
        Assert.False(repository.PersistedAutoStart);
        Assert.Equal(1, repository.SaveCallCount);
    }

    [Fact]
    public async Task SetEnabledAsync_WhenSettingUnchanged_AppliesRegistryButDoesNotSave()
    {
        FakeAutoStartService autoStart = new(initiallyEnabled: true);
        InMemorySettingsRepository repository = new(new Settings { AutoStart = true });
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        await coordinator.SetEnabledAsync(true);

        Assert.Equal(1, autoStart.EnableCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task SetEnabledAsync_WhenRegistryFails_PropagatesExceptionAndDoesNotSave()
    {
        FakeAutoStartService autoStart = new() { ThrowOnEnable = true };
        InMemorySettingsRepository repository = new();
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        _ = await Assert.ThrowsAsync<AutoStartException>(() => coordinator.SetEnabledAsync(true));
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task SynchronizeFromRegistryAsync_RegistryOnSettingsOff_AlignsSettingsAndCorrectsPath()
    {
        FakeAutoStartService autoStart = new(initiallyEnabled: true);
        InMemorySettingsRepository repository = new(new Settings { AutoStart = false });
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        bool result = await coordinator.SynchronizeFromRegistryAsync();

        Assert.True(result);
        Assert.True(repository.PersistedAutoStart);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.True(autoStart.EnableCallCount >= 1);
    }

    [Fact]
    public async Task SynchronizeFromRegistryAsync_RegistryOffSettingsOn_AlignsSettingsWithoutDisabling()
    {
        FakeAutoStartService autoStart = new(initiallyEnabled: false);
        InMemorySettingsRepository repository = new(new Settings { AutoStart = true });
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        bool result = await coordinator.SynchronizeFromRegistryAsync();

        Assert.False(result);
        Assert.False(repository.PersistedAutoStart);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal(0, autoStart.DisableCallCount);
    }

    [Fact]
    public async Task SynchronizeFromRegistryAsync_RegistryOnSettingsOn_CorrectsPathWithoutSaving()
    {
        FakeAutoStartService autoStart = new(initiallyEnabled: true);
        InMemorySettingsRepository repository = new(new Settings { AutoStart = true });
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        bool result = await coordinator.SynchronizeFromRegistryAsync();

        Assert.True(result);
        Assert.Equal(0, repository.SaveCallCount);
        Assert.Equal(1, autoStart.EnableCallCount);
    }

    [Fact]
    public async Task SynchronizeFromRegistryAsync_RegistryOffSettingsOff_DoesNothing()
    {
        FakeAutoStartService autoStart = new(initiallyEnabled: false);
        InMemorySettingsRepository repository = new(new Settings { AutoStart = false });
        AutoStartCoordinator coordinator = CreateCoordinator(autoStart, repository);

        bool result = await coordinator.SynchronizeFromRegistryAsync();

        Assert.False(result);
        Assert.Equal(0, repository.SaveCallCount);
        Assert.Equal(0, autoStart.EnableCallCount);
    }

    private static AutoStartCoordinator CreateCoordinator(
        FakeAutoStartService autoStart,
        InMemorySettingsRepository repository)
    {
        return new AutoStartCoordinator(
            autoStart,
            repository,
            NullLogger<AutoStartCoordinator>.Instance);
    }
}
