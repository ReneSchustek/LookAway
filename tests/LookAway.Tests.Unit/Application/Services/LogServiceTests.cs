using System.Collections.Generic;
using LookAway.Application.Services;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Tests.Unit.Application.Services;

/// <summary>
/// Tests fuer den <see cref="LogService"/>: pruefen Lifecycle-Logging
/// und Crash-Detection ueber einen <see cref="FakeCrashReporter"/>.
/// </summary>
public sealed class LogServiceTests
{
    [Fact]
    public void LogStart_ReturnsTrueWhenCrashReporterReportsUnresolvedCrash()
    {
        FakeCrashReporter crashReporter = new() { HasUnresolved = true };
        LogService service = new(NullLogger<LogService>.Instance, crashReporter);

        bool result = service.LogStart("1.2.3", Language.German);

        Assert.True(result);
    }

    [Fact]
    public void LogStart_ReturnsFalseWhenNoUnresolvedCrash()
    {
        FakeCrashReporter crashReporter = new() { HasUnresolved = false };
        LogService service = new(NullLogger<LogService>.Instance, crashReporter);

        bool result = service.LogStart("1.0.0", Language.English);

        Assert.False(result);
    }

    [Fact]
    public void LogStart_RejectsBlankVersion()
    {
        LogService service = new(NullLogger<LogService>.Instance, new FakeCrashReporter());

        _ = Assert.Throws<ArgumentException>(() => service.LogStart("  ", Language.German));
    }

    [Fact]
    public void LogShutdown_RejectsBlankReason()
    {
        LogService service = new(NullLogger<LogService>.Instance, new FakeCrashReporter());

        _ = Assert.Throws<ArgumentException>(() => service.LogShutdown(string.Empty));
    }

    [Fact]
    public void HandleUnhandledException_RoutesToCrashReporter()
    {
        FakeCrashReporter crashReporter = new();
        LogService service = new(NullLogger<LogService>.Instance, crashReporter);
        InvalidOperationException ex = new("kapow");

        service.HandleUnhandledException(ex, "TestSource");

        (Exception capturedException, string capturedSource) = Assert.Single(crashReporter.Reports);
        Assert.Same(ex, capturedException);
        Assert.Equal("TestSource", capturedSource);
    }

    [Fact]
    public void HandleUnhandledException_DoesNotThrowWhenCrashReporterFails()
    {
        ThrowingCrashReporter crashReporter = new();
        LogService service = new(NullLogger<LogService>.Instance, crashReporter);

        // Darf keine Exception nach aussen werfen.
        service.HandleUnhandledException(new InvalidOperationException("inner"), "TestSource");

        Assert.True(crashReporter.WasInvoked);
    }

    [Fact]
    public void AcknowledgeCrashes_ForwardsToCrashReporter()
    {
        FakeCrashReporter crashReporter = new();
        LogService service = new(NullLogger<LogService>.Instance, crashReporter);

        service.AcknowledgeCrashes();

        Assert.Equal(1, crashReporter.MarkResolvedCalls);
    }

    [Fact]
    public void Constructor_RejectsNullLogger()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => new LogService(null!, new FakeCrashReporter()));
    }

    [Fact]
    public void Constructor_RejectsNullCrashReporter()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => new LogService(NullLogger<LogService>.Instance, null!));
    }

    private sealed class FakeCrashReporter : ICrashReporter
    {
        public bool HasUnresolved { get; set; }

        public List<(Exception Exception, string Source)> Reports { get; } = [];

        public int MarkResolvedCalls { get; private set; }

        public void Report(Exception exception, string source) => Reports.Add((exception, source));

        public bool HasUnresolvedCrashes() => HasUnresolved;

        public void MarkResolved() => MarkResolvedCalls++;
    }

    private sealed class ThrowingCrashReporter : ICrashReporter
    {
        public bool WasInvoked { get; private set; }

        public void Report(Exception exception, string source)
        {
            WasInvoked = true;
            throw new IOException("disk full");
        }

        public bool HasUnresolvedCrashes() => false;

        public void MarkResolved()
        {
        }
    }
}
