namespace LookAway.Core.ValueObjects;

/// <summary>
/// Ergebnis eines erfolgreichen Downloads/Entpackens: der Staging-Ordner, die
/// Version und der SHA-256 der entpackten Programmdatei. Der Hash wird in den
/// Einstellungen vermerkt und vor dem Einspielen erneut geprüft.
/// </summary>
/// <param name="Directory">Staging-Ordner mit den neuen Dateien.</param>
/// <param name="Version">Versionszeichenkette des Updates.</param>
/// <param name="ExecutableSha256">SHA-256 (Hex) der entpackten <c>LookAway.exe</c>.</param>
public sealed record StagedUpdate(string Directory, string Version, string ExecutableSha256);
