namespace LookAway.Data.Tests;

/// <summary>
/// Liefert eindeutige, aber vorhersagbare IDs für Testdaten. Zufällige GUIDs machten einen
/// fehlgeschlagenen Lauf unwiederholbar; hier steht in jeder ID nur eine laufende Nummer.
/// </summary>
internal static class TestIds
{
    private static int _next;

    /// <summary>Gibt die nächste ID der Folge zurück (00000001-0000-…, 00000002-0000-…).</summary>
    /// <returns>Eine ID, die in diesem Testlauf noch nicht vergeben wurde.</returns>
    public static Guid Next() => new(Interlocked.Increment(ref _next), 0, 0, new byte[8]);
}
