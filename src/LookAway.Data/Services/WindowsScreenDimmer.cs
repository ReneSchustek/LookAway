using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Services;

/// <summary>
/// Dimmt DDC/CI-fähige Monitore über die Win32-Helligkeitssteuerung (Dxva2).
/// Auf Hardware ohne DDC/CI (z. B. viele Notebooks) bleibt der Aufruf
/// wirkungslos — Fehler werden geschluckt und geloggt.
/// </summary>
/// <remarks>
/// Von der Abdeckungsmessung ausgenommen: Ob das Dimmen greift, entscheidet der
/// angeschlossene Monitor. Das lässt sich nur am Gerät sehen.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Reine Systemanbindung ohne eigene Fachlogik.")]
public sealed partial class WindowsScreenDimmer : IScreenDimmer, IDisposable
{
    private readonly ILogger<WindowsScreenDimmer> _logger;
    private readonly List<MonitorBrightness> _dimmed = new();
    private bool _disposed;

    /// <summary>Erzeugt den Dimmer.</summary>
    /// <param name="logger">Logger für Hardware-/Interop-Fehler.</param>
    public WindowsScreenDimmer(ILogger<WindowsScreenDimmer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public void DimTo(int targetPercent)
    {
        if (_disposed)
        {
            return;
        }

        Restore(); // vorherigen Zustand sauber beenden, falls noch aktiv
        int clamped = Math.Clamp(targetPercent, 0, 100);

        try
        {
            foreach (PhysicalMonitor monitor in EnumeratePhysicalMonitors())
            {
                if (!TryDim(monitor, clamped))
                {
                    _ = DestroyPhysicalMonitor(monitor.Handle);
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
        {
            OnDimFailed(ex);
        }
    }

    // Dunkelt einen Monitor auf den Anteil seines Helligkeitsbereichs ab und merkt sich den alten
    // Wert. Monitore ohne regelbare Helligkeit (etwa ohne DDC/CI) lehnen ab.
    private bool TryDim(PhysicalMonitor monitor, int percent)
    {
        if (!GetMonitorBrightness(monitor.Handle, out uint min, out uint current, out uint max) || max <= min)
        {
            return false;
        }

        uint target = min + (uint)((max - min) * percent / 100.0);
        if (!SetMonitorBrightness(monitor.Handle, target))
        {
            return false;
        }

        _dimmed.Add(new MonitorBrightness(monitor.Handle, current));
        return true;
    }

    // Hardware-Fehler melden die Dxva2-Aufrufe über ihren Rückgabewert. Werfen können sie
    // nur, wenn die Bibliothek oder der Einsprungpunkt fehlt (Server-Kern, Windows-to-Go)
    // oder der Treiber im nativen Code fehlschlägt (SEHException).
    private void OnDimFailed(Exception exception)
    {
        ScreenDimmerLog.DimFailed(_logger, exception);
        Restore();
    }

    /// <inheritdoc />
    public void Restore()
    {
        foreach (MonitorBrightness monitor in _dimmed)
        {
            try
            {
                _ = SetMonitorBrightness(monitor.Handle, monitor.Original);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
            {
                ScreenDimmerLog.RestoreFailed(_logger, ex);
            }
            finally
            {
                _ = DestroyPhysicalMonitor(monitor.Handle);
            }
        }

        _dimmed.Clear();
    }

    private static List<PhysicalMonitor> EnumeratePhysicalMonitors()
    {
        List<PhysicalMonitor> result = new();
        List<nint> hMonitors = new();

        bool Callback(nint hMonitor, nint hdc, nint lprc, nint data)
        {
            hMonitors.Add(hMonitor);
            return true;
        }

        _ = EnumDisplayMonitors(0, 0, Callback, 0);

        foreach (nint hMonitor in hMonitors)
        {
            result.AddRange(PhysicalMonitorsOf(hMonitor));
        }

        return result;
    }

    // Die physischen Monitore hinter einem Anzeige-Monitor; ein gespiegelter Ausgang hat mehrere.
    private static IEnumerable<PhysicalMonitor> PhysicalMonitorsOf(nint hMonitor)
    {
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
        {
            return [];
        }

        PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
        return GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors)
            ? monitors.Select(monitor => new PhysicalMonitor(monitor.hPhysicalMonitor))
            : [];
    }

    /// <summary>Stellt die Helligkeit wieder her und gibt Ressourcen frei.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Restore();
        _disposed = true;
    }

    // Handle eines Monitors aus Dxva2; muss mit DestroyPhysicalMonitor freigegeben werden.
    private readonly record struct PhysicalMonitor(nint Handle);

    // Ein gedimmter Monitor samt der Helligkeit, die Restore zurückschreibt.
    private readonly record struct MonitorBrightness(nint Handle, uint Original);

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint lprcMonitor, nint data);

    // Gegenstück zur Win32-Struktur PHYSICAL_MONITOR; der Name folgt ihr, damit die
    // Zuordnung zur Dokumentation auf einen Blick stimmt.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public nint hPhysicalMonitor;

        // 128 = Win32-Konstante PHYSICAL_MONITOR_DESCRIPTION_SIZE.
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    // Bewusst klassisches [DllImport] statt des sonst genutzten source-generierten
    // [LibraryImport]: Der LibraryImport-Generator deckt das Delegate-Marshalling des
    // MonitorEnumProc-Callbacks (EnumDisplayMonitors) und das ByValTStr-Inline-String-Feld
    // in PHYSICAL_MONITOR nicht sauber ab — daher hier der klassische Marshaller.
    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("dxva2.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(nint hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetMonitorBrightness(nint hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetMonitorBrightness(nint hMonitor, uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool DestroyPhysicalMonitor(nint hMonitor);
}

/// <summary>
/// Source-generierte Logging-Methoden des Bildschirm-Dimmers.
/// </summary>
internal static partial class ScreenDimmerLog
{
    [LoggerMessage(EventId = 1700, Level = LogLevel.Information, Message = "Bildschirm-Dimmen nicht möglich (kein DDC/CI-fähiger Monitor?).")]
    public static partial void DimFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Warning, Message = "Bildschirmhelligkeit konnte nicht wiederhergestellt werden.")]
    public static partial void RestoreFailed(ILogger logger, Exception exception);
}
