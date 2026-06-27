using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Liest die Benutzer-Inaktivitaetsdauer ueber die Win32-API
/// <c>GetLastInputInfo</c>. Erste Stelle mit Plattform-spezifischem P/Invoke;
/// bewusst in der Data-/Infrastructure-Schicht isoliert.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsIdleDetector : IIdleDetector
{
    /// <inheritdoc />
    public TimeSpan GetIdleTime()
    {
        LastInputInfo info = new()
        {
            CbSize = (uint)Marshal.SizeOf<LastInputInfo>(),
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        // dwTime ist ein 32-Bit-Tick-Count seit Systemstart. Die ungepruefte
        // uint-Subtraktion behandelt den ~49-Tage-Ueberlauf korrekt; daher
        // bewusst Environment.TickCount (32 Bit), nicht TickCount64.
        uint idleMilliseconds = unchecked((uint)Environment.TickCount) - info.DwTime;
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }
}
