using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Hält ein Fenster über <c>SetWindowPos</c> an der Spitze der obersten Fensterebene.
/// </summary>
/// <remarks>
/// Warum der Aufruf nötig ist, obwohl WinUI mit <c>IsAlwaysOnTop</c> eine eigene
/// Einstellung mitbringt: Diese setzt das Fenster einmalig in die oberste Ebene, ordnet
/// es darin aber nicht nach. Kommt dort ein weiteres Fenster hinzu — die Taskleiste, ein
/// Anruf-Fenster, eine Benachrichtigung —, liegt es über dem Overlay. Ein erneutes
/// <c>SetWindowPos</c> hebt das Overlay wieder nach oben, ohne den Eingabefokus zu
/// bewegen (<c>SWP_NOACTIVATE</c>).
///
/// Von der Abdeckungsmessung ausgenommen: Die Klasse reicht einen Win32-Aufruf durch;
/// ob er wirkt, entscheidet die Fensterverwaltung des Systems.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Reine Systemanbindung ohne eigene Fachlogik.")]
[SupportedOSPlatform("windows")]
public sealed partial class WindowsTopmostWindowGuard : ITopmostWindowGuard
{
    private static readonly nint HwndTopmost = -1;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    /// <inheritdoc />
    public void BringToTop(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return;
        }

        // Das Ergebnis wird bewusst nicht ausgewertet: Der Aufruf läuft im Sekundentakt
        // der Pause. Schlägt er einmal fehl (etwa weil das Fenster gerade geschlossen
        // wird), ist der nächste Takt die Antwort darauf — eine Meldung je Sekunde wäre
        // nur Rauschen im Protokoll.
        _ = SetWindowPos(windowHandle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
