using BenchmarkDotNet.Attributes;

namespace LookAway.Benchmarks;

/// <summary>
/// Grundlage der Messungen, die ihren Bestand in einem eigenen temporären Ordner anlegen und ihn
/// am Ende wieder abräumen.
/// </summary>
/// <remarks>
/// Eine Basisklasse statt des Musters in jeder Messung: BenchmarkDotNet verlangt nicht versiegelte
/// Klassen, und für die gilt das vollständige Dispose-Muster.
/// </remarks>
public abstract class TemporaryDirectoryBenchmark : IDisposable
{
    /// <summary>Der Ordner des Bestands; erst nach <see cref="CreateDirectory"/> gesetzt.</summary>
    protected string Directory { get; private set; } = string.Empty;

    /// <summary>Räumt den Bestand ab.</summary>
    [GlobalCleanup]
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Legt einen frischen Ordner unter dem temporären Verzeichnis an.</summary>
    /// <param name="purpose">Kurzer Name des Bestands, erscheint im Ordnernamen.</param>
    /// <returns>Pfad des Ordners.</returns>
    protected string CreateDirectory(string purpose)
    {
        Directory = Path.Combine(Path.GetTempPath(), "lookaway-benchmarks", $"{purpose}-{Guid.NewGuid():N}");
        _ = System.IO.Directory.CreateDirectory(Directory);
        return Directory;
    }

    /// <summary>Gibt die Ressourcen der Messung frei und löscht den Ordner.</summary>
    /// <param name="disposing"><c>true</c> beim Aufruf über <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
