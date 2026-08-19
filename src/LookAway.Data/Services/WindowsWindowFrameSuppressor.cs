using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Nimmt einem Fenster über <c>DwmSetWindowAttribute</c> die Randlinie und die
/// abgerundeten Ecken.
/// </summary>
/// <remarks>
/// Warum das nötig ist, obwohl der Presenter Rahmen und Titelleiste bereits abschaltet:
/// <c>SetBorderAndTitleBar(false, false)</c> betrifft den Fensterrahmen der Anwendung. Was
/// darüber hinaus bleibt, zeichnet die Fensterverwaltung (DWM) selbst — eine zwei bis drei
/// Bildpunkte breite Linie in Systemfarbe und vier runde Ecken, durch die der Desktop
/// schaut. Bei einem bildschirmfüllenden Overlay ist beides sichtbar, und die Linie trägt
/// nicht die eingestellte Overlay-Farbe.
///
/// Beide Einstellungen gibt es ab Windows 11 (Build 22000). Ältere Fassungen antworten mit
/// <c>E_INVALIDARG</c>; der Rückgabewert wird deshalb bewusst nicht ausgewertet, und der
/// Schmuck bleibt dort stehen (siehe <see cref="IWindowFrameSuppressor"/>).
///
/// Von der Abdeckungsmessung ausgenommen: Die Klasse reicht zwei Win32-Aufrufe durch; ob sie
/// wirken, entscheidet die Fensterverwaltung des Systems.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Reine Systemanbindung ohne eigene Fachlogik.")]
[SupportedOSPlatform("windows")]
public sealed partial class WindowsWindowFrameSuppressor : IWindowFrameSuppressor
{
    private const int WindowCornerPreference = 33;
    private const int DoNotRound = 1;

    private const int BorderColor = 34;
    private const int ColorNone = unchecked((int)0xFFFFFFFE);

    private const int StyleIndex = -16;
    private const int ExtendedStyleIndex = -20;

    // Rahmenstile, die einen nichtklientischen Rand zeichnen lassen.
    private const nint FrameStyles = 0x00C00000 | 0x00040000;   // WS_CAPTION (inkl. WS_BORDER/WS_DLGFRAME) | WS_THICKFRAME
    private const nint FrameExtendedStyles = 0x00000100 | 0x00000200 | 0x00020000;   // WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    /// <inheritdoc />
    public void SuppressFrame(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return;
        }

        SetAttribute(windowHandle, WindowCornerPreference, DoNotRound);
        SetAttribute(windowHandle, BorderColor, ColorNone);
        RemoveFrameStyles(windowHandle);
    }

    // Gemessen an drei Monitoren: Der Presenter lässt WS_DLGFRAME und WS_EX_WINDOWEDGE
    // stehen, und die Fensterverwaltung zeichnet dafür eine helle Linie am Rand — auch
    // ohne Titelleiste und ohne DWM-Rahmenfarbe. Erst ohne diese Stile reicht die Fläche
    // bis an den äußersten Bildpunkt.
    private static void RemoveFrameStyles(nint windowHandle)
    {
        nint style = GetWindowLongPtr(windowHandle, StyleIndex);
        nint extended = GetWindowLongPtr(windowHandle, ExtendedStyleIndex);

        _ = SetWindowLongPtr(windowHandle, StyleIndex, style & ~FrameStyles);
        _ = SetWindowLongPtr(windowHandle, ExtendedStyleIndex, extended & ~FrameExtendedStyles);

        // Ohne SWP_FRAMECHANGED merkt das Fenster die geänderten Stile erst beim nächsten
        // Größenwechsel — der bei einem Overlay nie kommt.
        _ = SetWindowPos(
            windowHandle,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private static void SetAttribute(nint windowHandle, int attribute, int value)
    {
        int buffer = value;
        _ = DwmSetWindowAttribute(windowHandle, attribute, ref buffer, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
