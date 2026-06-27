using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Erkennt eine Vollbild-Anwendung im Vordergrund ueber Win32-APIs
/// (<c>GetForegroundWindow</c> + Vergleich von Fenster- und Monitor-Rechteck).
/// Shell-Fenster (Desktop, Sperrbildschirm) werden ausgeschlossen.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsFullscreenDetector : IFullscreenDetector
{
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

        if (!GetWindowRect(foreground, out Rect windowRect))
        {
            return false;
        }

        nint monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
        MonitorInfo monitorInfo = new()
        {
            CbSize = (uint)Marshal.SizeOf<MonitorInfo>(),
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        Rect screen = monitorInfo.RcMonitor;
        return windowRect.Left <= screen.Left
            && windowRect.Top <= screen.Top
            && windowRect.Right >= screen.Right
            && windowRect.Bottom >= screen.Bottom;
    }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
    }
}
