using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LookAway.Data.Tests;

/// <summary>
/// Verbietet dem angemeldeten Benutzer, einen Ordner aufzulisten, bis zum Entsorgen. Jedes
/// Aufzählen des Ordners läuft in eine <see cref="UnauthorizedAccessException"/>.
/// </summary>
/// <remarks>
/// Wie <see cref="LockedFile"/>: Das Versprechen, Rechtefehler abzufangen, lässt sich nur prüfen,
/// wenn der Fehler wirklich eintritt. Als Eigentümer darf der Benutzer die Regel jederzeit wieder
/// entfernen, der Ordner bleibt also aufräumbar.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class UnlistableFolder : IDisposable
{
    private readonly DirectoryInfo _folder;
    private readonly FileSystemAccessRule _denyListing;

    /// <summary>Setzt die Sperre auf einen vorhandenen Ordner.</summary>
    /// <param name="path">Pfad des Ordners.</param>
    public UnlistableFolder(string path)
    {
        _folder = new DirectoryInfo(path);
        _denyListing = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!, // ein angemeldeter Benutzer hat immer eine SID
            FileSystemRights.ListDirectory,
            AccessControlType.Deny);

        DirectorySecurity security = _folder.GetAccessControl();
        security.AddAccessRule(_denyListing);
        _folder.SetAccessControl(security);
    }

    public void Dispose()
    {
        DirectorySecurity security = _folder.GetAccessControl();
        _ = security.RemoveAccessRule(_denyListing);
        _folder.SetAccessControl(security);
    }
}
