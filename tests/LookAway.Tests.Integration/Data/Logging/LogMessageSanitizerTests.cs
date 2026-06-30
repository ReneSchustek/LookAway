using LookAway.Data.Logging;

namespace LookAway.Tests.Integration.Data.Logging;

/// <summary>
/// Tests für den <see cref="LogMessageSanitizer"/>: stellt sicher, dass
/// benutzerspezifische Pfade und der Benutzername durch generische
/// Platzhalter ersetzt werden.
/// </summary>
public sealed class LogMessageSanitizerTests
{
    private const string LocalAppData = @"C:\Users\testuser\AppData\Local";
    private const string AppData = @"C:\Users\testuser\AppData\Roaming";
    private const string UserProfile = @"C:\Users\testuser";
    private const string UserName = "testuser";

    [Fact]
    public void Sanitize_ReplacesAppDataPath()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        string result = sanitizer.Sanitize($@"Settings gespeichert in {AppData}\LookAway\settings.json");

        Assert.Equal(@"Settings gespeichert in %APPDATA%\LookAway\settings.json", result);
    }

    [Fact]
    public void Sanitize_ReplacesLocalAppDataBeforeUserProfile()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        string result = sanitizer.Sanitize($@"Cache: {LocalAppData}\LookAway");

        Assert.Equal(@"Cache: %LOCALAPPDATA%\LookAway", result);
    }

    [Fact]
    public void Sanitize_ReplacesUserProfilePath()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        string result = sanitizer.Sanitize($@"Home: {UserProfile}\Documents\file.txt");

        Assert.Equal(@"Home: %USERPROFILE%\Documents\file.txt", result);
    }

    [Fact]
    public void Sanitize_ReplacesUserNameAfterPaths()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        string result = sanitizer.Sanitize($"User {UserName} hat sich angemeldet");

        Assert.Equal("User <user> hat sich angemeldet", result);
    }

    [Fact]
    public void Sanitize_IsCaseInsensitive()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        string result = sanitizer.Sanitize(AppData.ToUpperInvariant() + @"\foo");

        Assert.Equal(@"%APPDATA%\foo", result);
    }

    [Fact]
    public void Sanitize_NullOrEmptyReturnsEmptyString()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        Assert.Equal(string.Empty, sanitizer.Sanitize(null));
        Assert.Equal(string.Empty, sanitizer.Sanitize(string.Empty));
    }

    [Fact]
    public void Sanitize_LongerPathTakesPrecedenceOverShorter()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        string result = sanitizer.Sanitize($@"Datei {LocalAppData}\Temp\x.tmp im User-Verzeichnis {UserProfile}");

        Assert.Equal(@"Datei %LOCALAPPDATA%\Temp\x.tmp im User-Verzeichnis %USERPROFILE%", result);
    }

    [Fact]
    public void Sanitize_DoesNotReplaceSubstringsThatAreNotPresent()
    {
        LogMessageSanitizer sanitizer = new(LocalAppData, AppData, UserProfile, UserName);

        string result = sanitizer.Sanitize("Keine sensiblen Daten in dieser Nachricht");

        Assert.Equal("Keine sensiblen Daten in dieser Nachricht", result);
    }
}
