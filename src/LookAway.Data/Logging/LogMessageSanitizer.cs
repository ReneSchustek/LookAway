namespace LookAway.Data.Logging;

/// <summary>
/// Maskiert benutzerspezifische Pfade und den Windows-Benutzernamen in
/// Log-Messages, bevor sie persistiert werden.
/// </summary>
/// <remarks>
/// Die Sanitisierung erfolgt rein per String-Replacement. Ersetzungen sind
/// nach absteigender Pfadlaenge sortiert, damit z. B. <c>%LOCALAPPDATA%</c>
/// vor <c>%USERPROFILE%</c> erkannt wird. Der Sanitizer ist threadsafe,
/// weil er nur unveraenderliche Felder liest.
/// </remarks>
public sealed class LogMessageSanitizer
{
    private readonly (string Original, string Replacement)[] _replacements;

    /// <summary>
    /// Erzeugt einen Sanitizer fuer den aktuellen Windows-Benutzer.
    /// </summary>
    public LogMessageSanitizer()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.UserName)
    {
    }

    /// <summary>
    /// Konstruktor fuer Tests: erlaubt das Setzen aller benutzerspezifischen
    /// Werte unabhaengig vom aktuellen Prozess-Kontext.
    /// </summary>
    /// <param name="localAppData">Pfad zu <c>%LOCALAPPDATA%</c>.</param>
    /// <param name="appData">Pfad zu <c>%APPDATA%</c>.</param>
    /// <param name="userProfile">Pfad zu <c>%USERPROFILE%</c>.</param>
    /// <param name="userName">Aktueller Windows-Benutzername.</param>
    public LogMessageSanitizer(string localAppData, string appData, string userProfile, string userName)
    {
        ArgumentNullException.ThrowIfNull(localAppData);
        ArgumentNullException.ThrowIfNull(appData);
        ArgumentNullException.ThrowIfNull(userProfile);
        ArgumentNullException.ThrowIfNull(userName);

        // Sortiert nach Laenge absteigend, damit Pfade vor dem Benutzernamen
        // ersetzt werden und laengere Pfade vor kuerzeren.
        List<(string Original, string Replacement)> entries = [];
        AddIfPresent(entries, localAppData, "%LOCALAPPDATA%");
        AddIfPresent(entries, appData, "%APPDATA%");
        AddIfPresent(entries, userProfile, "%USERPROFILE%");
        AddIfPresent(entries, userName, "<user>");

        _replacements = [.. entries.OrderByDescending(e => e.Original.Length)];
    }

    /// <summary>
    /// Ersetzt benutzerspezifische Pfade und den Benutzernamen in der
    /// uebergebenen Zeichenkette durch generische Platzhalter.
    /// </summary>
    /// <param name="input">Zu maskierende Eingabe; <c>null</c> ergibt eine leere Zeichenkette.</param>
    public string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string result = input;
        foreach ((string original, string replacement) in _replacements)
        {
            result = result.Replace(original, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static void AddIfPresent(
        List<(string Original, string Replacement)> entries,
        string original,
        string replacement)
    {
        if (!string.IsNullOrEmpty(original))
        {
            entries.Add((original, replacement));
        }
    }
}
