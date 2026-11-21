using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Erkennt eine Vollbild-Anwendung im Vordergrund über Win32-APIs
/// (<c>GetForegroundWindow</c> + Vergleich von Fenster- und Monitor-Rechteck).
/// Shell-Fenster (Desktop, Sperrbildschirm) werden ausgeschlossen.
/// </summary>
/// <remarks>
/// Von der Abdeckungsmessung ausgenommen: Die Entscheidung stützt sich auf die Lage
/// des Vordergrundfensters gegenüber dem Monitor. Beides liefert das Betriebssystem;
/// ohne echten Bildschirm bliebe von der Prüfung nichts übrig.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Reine Systemanbindung ohne eigene Fachlogik.")]
[SupportedOSPlatform("windows")]
public sealed partial class WindowsFullscreenDetector : IFullscreenDetector
{
    // MONITOR_DEFAULTTONEAREST: Liegt das Fenster auf keinem Monitor, zählt der nächstgelegene.
    // Sonst liefert MonitorFromWindow ohnehin den mit der größten Überschneidung.
    private const uint MonitorDefaultToNearest = 0x00000002;

    /// <inheritdoc />
    public bool IsFullscreenApplicationActive()
    {
        nint foreground = GetForegroundWindow();
        if (foreground == nint.Zero)
        {
            return false;
        }

        if (foreground == GetDesktopWindow() || foreground == GetShellWindow())
        {
            return false;
        }

        return GetWindowRect(foreground, out Rect windowRect)
            && TryGetMonitorBounds(foreground, out Rect screen)
            && Covers(windowRect, screen);
    }

    // Grenzen des Monitors, auf dem das Fenster überwiegend liegt.
    private static bool TryGetMonitorBounds(nint window, out Rect bounds)
    {
        MonitorInfo monitorInfo = new()
        {
            CbSize = (uint)Marshal.SizeOf<MonitorInfo>(),
        };

        bool found = GetMonitorInfo(MonitorFromWindow(window, MonitorDefaultToNearest), ref monitorInfo);
        bounds = monitorInfo.RcMonitor;
        return found;
    }

    private static bool Covers(Rect window, Rect screen)
        => window.Left <= screen.Left
            && window.Top <= screen.Top
            && window.Right >= screen.Right
            && window.Bottom >= screen.Bottom;

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetDesktopWindow();

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetShellWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint hMonitor, ref MonitorInfo lpmi);

    // Gegenstücke zu den Win32-Strukturen RECT und MONITORINFO; Reihenfolge und Breite
    // der Felder sind vorgegeben.
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Nur RcMonitor wird gelesen; die übrigen Felder braucht die Struktur für ihre Größe.
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
    }
}
