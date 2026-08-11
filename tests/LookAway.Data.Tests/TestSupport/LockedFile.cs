namespace LookAway.Data.Tests;

/// <summary>
/// Hält eine Datei so geöffnet, dass niemand sonst sie lesen, schreiben oder löschen
/// kann. Jeder Zugriff von außen läuft in eine <see cref="IOException"/>.
/// </summary>
/// <remarks>
/// Die Ablagen der Anwendung versprechen, Dateifehler zu schlucken statt sie
/// durchzureichen — ein Programm, das beim Zugriff auf eine gerade gesicherte Datei
/// abstürzt, wäre unbrauchbar. Dieses Versprechen lässt sich nur prüfen, wenn der
/// Fehler auch wirklich eintritt. Eine Attrappe des Dateisystems würde stattdessen
/// nur belegen, dass die Attrappe wirft.
/// </remarks>
internal sealed class LockedFile : IDisposable
{
    private readonly FileStream _stream;

    /// <summary>Legt die Datei an, falls nötig, und sperrt sie.</summary>
    /// <param name="path">Pfad zur zu sperrenden Datei.</param>
    /// <param name="content">Inhalt, der vor dem Sperren geschrieben wird.</param>
    public LockedFile(string path, string content = "")
    {
        File.WriteAllText(path, content);
        _stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    public void Dispose() => _stream.Dispose();
}
