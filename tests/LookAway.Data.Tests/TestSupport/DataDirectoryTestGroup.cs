namespace LookAway.Data.Tests;

/// <summary>
/// Name der Testgruppe für alles, was den Basisordner von <see cref="AppDataLocation"/> liest
/// oder umstellt.
/// </summary>
/// <remarks>
/// Der Basisordner ist prozessweiter Zustand. Tests derselben Gruppe laufen nacheinander; liefen
/// sie parallel, sähe einer den portablen Ordner des anderen statt des Datenordners im
/// Benutzerprofil. Andere Gruppen lesen den Basisordner nicht und dürfen daneben weiterlaufen.
/// </remarks>
internal static class DataDirectoryTestGroup
{
    /// <summary>Name der Testgruppe.</summary>
    public const string Name = "Datenverzeichnis";
}
