using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LookAway.Services;

/// <summary>
/// Dimmt DDC/CI-faehige Monitore ueber die Win32-Helligkeitssteuerung (Dxva2,
///). Auf Hardware ohne DDC/CI (z. B. viele Notebooks) bleibt der Aufruf
/// wirkungslos — Fehler werden geschluckt und geloggt.
/// </summary>
internal sealed partial class WindowsScreenDimmer : IScreenDimmer, IDisposable
{
    private readonly ILogger<WindowsScreenDimmer> _logger;
    private readonly List<MonitorBrightness> _dimmed = new();
    private bool _disposed;

    /// <summary>Erzeugt den Dimmer.</summary>
    /// <param name="logger">Logger fuer Hardware-/Interop-Fehler.</param>
    public WindowsScreenDimmer(ILogger<WindowsScreenDimmer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Helligkeitssteuerung ist unkritisch: jeder Hardware-/Interop-Fehler wird geloggt und ignoriert, damit die App nie abstuerzt.")]
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
                if (GetMonitorBrightness(monitor.Handle, out uint min, out uint current, out uint max) && max > min)
                {
                    uint target = min + (uint)((max - min) * clamped / 100.0);
                    if (SetMonitorBrightness(monitor.Handle, target))
                    {
                        _dimmed.Add(new MonitorBrightness(monitor.Handle, current));
                        continue;
                    }
                }

                _ = DestroyPhysicalMonitor(monitor.Handle);
            }
        }
        catch (Exception ex)
        {
            ScreenDimmerLog.DimFailed(_logger, ex);
            Restore();
        }
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Wiederherstellung ist unkritisch: Fehler werden geloggt und ignoriert.")]
    public void Restore()
    {
        foreach (MonitorBrightness monitor in _dimmed)
        {
            try
            {
                _ = SetMonitorBrightness(monitor.Handle, monitor.Original);
            }
            catch (Exception ex)
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
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
            {
                continue;
            }

            PHYSICAL_MONITOR[] monitors = new PHYSICAL_MONITOR[count];
            if (GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
            {
                foreach (PHYSICAL_MONITOR monitor in monitors)
                {
                    result.Add(new PhysicalMonitor(monitor.hPhysicalMonitor));
                }
            }
        }

        return result;
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

    private readonly record struct PhysicalMonitor(nint Handle);

    private readonly record struct MonitorBrightness(nint Handle, uint Original);

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint lprcMonitor, nint data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public nint hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

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
    [LoggerMessage(EventId = 1700, Level = LogLevel.Information, Message = "Bildschirm-Dimmen nicht moeglich (kein DDC/CI-faehiger Monitor?).")]
    public static partial void DimFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Warning, Message = "Bildschirmhelligkeit konnte nicht wiederhergestellt werden.")]
    public static partial void RestoreFailed(ILogger logger, Exception exception);
}
