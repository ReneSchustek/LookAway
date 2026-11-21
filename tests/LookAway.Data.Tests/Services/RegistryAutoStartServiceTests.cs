using LookAway.Core.Exceptions;
using System.Runtime.Versioning;
using LookAway.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace LookAway.Data.Tests;

/// <summary>
/// Integrationstests für den <see cref="RegistryAutoStartService"/> gegen die
/// echte Registry (<c>HKCU\…\Run</c>). Jeder Test verwendet einen eindeutigen
/// Eintragsnamen und räumt ihn in <see cref="Dispose"/> wieder ab — es werden
/// nur <c>HKCU</c>-Schlüssel berührt, daher keine Administrator-Rechte nötig.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryAutoStartServiceTests : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;

    /// <summary>
    /// Erzeugt für jeden Test einen eindeutigen Eintragsnamen, damit ein real
    /// vorhandener <c>LookAway</c>-Autostart des Entwicklers nicht berührt wird.
    /// </summary>
    public RegistryAutoStartServiceTests()
    {
        _valueName = "LookAway.IT." + Guid.NewGuid().ToString("N");
    }

    /// <summary>Entfernt den Test-Eintrag, falls er noch vorhanden ist.</summary>
    public void Dispose()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    [Fact]
    public void IsEnabled_WithoutEntry_ReturnsFalse()
    {
        RegistryAutoStartService service = CreateService(@"C:\Tools\LookAway\LookAway.App.exe");

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Enable_CreatesQuotedEntry_AndIsEnabledReturnsTrue()
    {
        const string executablePath = @"C:\Program Files\LookAway\LookAway.App.exe";
        RegistryAutoStartService service = CreateService(executablePath);

        service.Enable();

        Assert.True(service.IsEnabled());
        string? stored = ReadRawValue();
        Assert.NotNull(stored);
        Assert.StartsWith("\"" + executablePath + "\"", stored, StringComparison.Ordinal);
    }

    [Fact]
    public void Disable_RemovesEntry_AndIsEnabledReturnsFalse()
    {
        RegistryAutoStartService service = CreateService(@"C:\Program Files\LookAway\LookAway.App.exe");
        service.Enable();
        Assert.True(service.IsEnabled());

        service.Disable();

        Assert.False(service.IsEnabled());
        Assert.Null(ReadRawValue());
    }

    [Fact]
    public void Disable_WithoutExistingEntry_DoesNotThrow()
    {
        RegistryAutoStartService service = CreateService(@"C:\Program Files\LookAway\LookAway.App.exe");

        service.Disable();

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Enable_WhenAlreadyCurrent_KeepsSameValue()
    {
        const string executablePath = @"C:\Program Files\LookAway\LookAway.App.exe";
        RegistryAutoStartService service = CreateService(executablePath);

        service.Enable();
        string? firstWrite = ReadRawValue();
        service.Enable();
        string? secondWrite = ReadRawValue();

        Assert.Equal(firstWrite, secondWrite);
    }

    [Fact]
    public void Enable_WhenPathChanged_UpdatesEntryToCurrentPath()
    {
        const string oldPath = @"C:\Old Location\LookAway\LookAway.App.exe";
        const string newPath = @"C:\New Location\LookAway\LookAway.App.exe";

        CreateService(oldPath).Enable();
        string? before = ReadRawValue();

        CreateService(newPath).Enable();
        string? after = ReadRawValue();

        Assert.NotNull(after);
        Assert.NotEqual(before, after);
        Assert.StartsWith("\"" + newPath + "\"", after, StringComparison.Ordinal);
    }

    private RegistryAutoStartService CreateService(string executablePath)
    {
        return new RegistryAutoStartService(
            _valueName,
            () => executablePath,
            NullLogger<RegistryAutoStartService>.Instance);
    }

    private string? ReadRawValue()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return runKey?.GetValue(_valueName) as string;
    }

    /// <remarks>
    /// Richtlinie, fehlende Rechte und E/A-Fehler lassen sich an der echten Registry nicht
    /// gezielt auslösen. Die Übersetzung ist deshalb eine eigene Methode und wird hier direkt
    /// mit einem werfenden Zugriff geprüft.
    /// </remarks>
    [Theory]
    [InlineData(typeof(System.Security.SecurityException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    public void RegistryErrors_BecomeAnAutoStartExceptionWithTheCause(Type errorType)
    {
        Exception cause = (Exception)Activator.CreateInstance(errorType, "Ursache")!; // alle drei haben einen Konstruktor mit Meldung

        AutoStartException thrown = Assert.Throws<AutoStartException>(
            () => RegistryAutoStartService.TranslateRegistryErrors<bool>("Meldung", () => throw cause));

        Assert.Equal("Meldung", thrown.Message);
        Assert.Same(cause, thrown.InnerException);
    }

    [Fact]
    public void OtherErrors_PassThroughUnchanged()
        => Assert.Throws<InvalidOperationException>(
            () => RegistryAutoStartService.TranslateRegistryErrors<bool>("Meldung", () => throw new InvalidOperationException()));

    [Fact]
    public void ResolveCurrentExecutablePath_NamesAnExistingFile()
        => Assert.True(File.Exists(RegistryAutoStartService.ResolveCurrentExecutablePath()));
}
