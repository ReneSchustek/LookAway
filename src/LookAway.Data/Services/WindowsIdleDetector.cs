using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LookAway.Core.Interfaces;

namespace LookAway.Data.Services;

/// <summary>
/// Liest die Benutzer-Inaktivitätsdauer über die Win32-API
/// <c>GetLastInputInfo</c>. Erste Stelle mit Plattform-spezifischem P/Invoke;
/// bewusst in der Data-/Infrastructure-Schicht isoliert.
/// </summary>
/// <remarks>
/// Von der Abdeckungsmessung ausgenommen: Die Klasse reicht einen einzelnen
/// Win32-Aufruf durch. Was hier schiefgehen kann, liegt jenseits der Grenze — ein
/// Test könnte nur die eigene Attrappe messen.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Reine Systemanbindung ohne eigene Fachlogik.")]
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

        // dwTime ist ein 32-Bit-Tick-Count seit Systemstart. Die ungeprüfte
        // uint-Subtraktion behandelt den ~49-Tage-Überlauf korrekt; daher
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
