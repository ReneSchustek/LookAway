using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Services;

/// <summary>
/// Registriert globale Hotkeys ueber die Win32-API <c>RegisterHotKey</c>.
/// Ein eigener Hintergrund-Thread betreibt ein nachrichtenfreies Fenster
/// (<c>HWND_MESSAGE</c>) mit Message-Loop und empfaengt <c>WM_HOTKEY</c>.
/// </summary>
/// <remarks>
/// Registrierung und Freigabe laufen auf dem Thread, dem das Fenster gehoert;
/// Aufrufe von aussen werden ueber eine gepostete Nachricht dorthin marshalled.
/// </remarks>
[SuppressMessage(
    "Usage",
    "CA2216:Disposable types should declare finalizer",
    Justification = "Das einzige native Handle (Nachrichtenfenster) gehoert einem Hintergrund-Thread mit Prozess-Lebensdauer und wird in Dispose freigegeben; ein Finalizer koennte nicht sicher auf diesen Thread marshallen.")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed partial class WindowsHotkeyService : IHotkeyService, IDisposable
{
    private const uint WmHotkey = 0x0312;
    private const uint WmApplyBindings = 0x0400; // WM_APP
    private const uint WmQuitLoop = 0x0401;      // WM_APP + 1 — beendet den Message-Loop
    private const uint ModNoRepeat = 0x4000;
    private const int GwlpWndProc = -4;

    private static readonly nint HwndMessage = -3;

    private readonly ILogger<WindowsHotkeyService> _logger;
    private readonly Lock _gate = new();
    private readonly List<HotkeyAction> _registered = new();

    private Dictionary<HotkeyAction, HotkeyDefinition> _pending = new();
    private Thread? _thread;
    private nint _hwnd;
    private nint _originalWndProc;
    private WndProcDelegate? _wndProc; // Feld haelt das Delegate vor dem GC.
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Service und startet den Nachrichten-Thread.
    /// </summary>
    /// <param name="logger">Logger fuer Registrierungs-Vorgaenge.</param>
    public WindowsHotkeyService(ILogger<WindowsHotkeyService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "LookAway-Hotkeys",
        };
        _thread.Start();
    }

    /// <inheritdoc />
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <inheritdoc />
    public void Register(IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            _pending = new Dictionary<HotkeyAction, HotkeyDefinition>(bindings);
        }

        SignalApply();
    }

    /// <inheritdoc />
    public void UnregisterAll()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            _pending = new Dictionary<HotkeyAction, HotkeyDefinition>();
        }

        SignalApply();
    }

    private void SignalApply()
    {
        nint hwnd = Volatile.Read(ref _hwnd);
        if (hwnd != 0)
        {
            _ = PostMessageW(hwnd, WmApplyBindings, 0, 0);
        }
    }

    private void RunMessageLoop()
    {
        nint hwnd = CreateWindowExW(0, "STATIC", null, 0, 0, 0, 0, 0, HwndMessage, 0, 0, 0);
        if (hwnd == 0)
        {
            WindowsHotkeyServiceLog.WindowCreationFailed(_logger);
            return;
        }

        _wndProc = WindowProc;
        _originalWndProc = SetWindowLongPtrW(hwnd, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_wndProc));
        Volatile.Write(ref _hwnd, hwnd);

        // Falls vor dem Fensteraufbau bereits Bindings gesetzt wurden.
        ApplyPending();

        while (GetMessageW(out Msg message, 0, 0, 0) > 0)
        {
            _ = TranslateMessage(ref message);
            _ = DispatchMessageW(ref message);
        }
    }

    private nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmHotkey:
                OnHotkey((int)wParam);
                return 0;
            case WmApplyBindings:
                ApplyPending();
                return 0;
            case WmQuitLoop:
                // Abbau auf dem Thread, dem das Fenster gehoert (Hotkeys sind thread-affin).
                TeardownOnMessageThread(hWnd);
                return 0;
            default:
                return CallWindowProcW(_originalWndProc, hWnd, msg, wParam, lParam);
        }
    }

    private void TeardownOnMessageThread(nint hWnd)
    {
        foreach (HotkeyAction action in _registered)
        {
            _ = UnregisterHotKey(hWnd, (int)action);
        }

        _registered.Clear();
        _ = DestroyWindow(hWnd);
        PostQuitMessage(0);
    }

    private void OnHotkey(int id)
    {
        if (Enum.IsDefined(typeof(HotkeyAction), id))
        {
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs((HotkeyAction)id));
        }
    }

    private void ApplyPending()
    {
        foreach (HotkeyAction action in _registered)
        {
            _ = UnregisterHotKey(_hwnd, (int)action);
        }

        _registered.Clear();

        Dictionary<HotkeyAction, HotkeyDefinition> snapshot;
        lock (_gate)
        {
            snapshot = new Dictionary<HotkeyAction, HotkeyDefinition>(_pending);
        }

        foreach ((HotkeyAction action, HotkeyDefinition definition) in snapshot)
        {
            if (!HotkeyValidator.IsValid(definition))
            {
                continue;
            }

            uint modifiers = (uint)definition.Modifiers | ModNoRepeat;
            if (RegisterHotKey(_hwnd, (int)action, modifiers, (uint)definition.VirtualKey))
            {
                _registered.Add(action);
            }
            else
            {
                WindowsHotkeyServiceLog.RegistrationFailed(_logger, action.ToString());
            }
        }
    }

    /// <summary>Gibt alle Hotkeys frei und beendet den Nachrichten-Thread.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Abbau auf dem Message-Thread anstossen (Hotkeys sind thread-affin); der
        // Handler gibt die Hotkeys frei, zerstoert das Fenster und beendet den Loop.
        nint hwnd = Volatile.Read(ref _hwnd);
        if (hwnd != 0)
        {
            _ = PostMessageW(hwnd, WmQuitLoop, 0, 0);
        }

        _ = _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int width,
        int height,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint CallWindowProcW(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetMessageW(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TranslateMessage(ref Msg lpMsg);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint DispatchMessageW(ref Msg lpMsg);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);
}

/// <summary>
/// Source-generierte Logging-Methoden des Hotkey-Service.
/// </summary>
internal static partial class WindowsHotkeyServiceLog
{
    [LoggerMessage(EventId = 1500, Level = LogLevel.Warning, Message = "Hotkey-Nachrichtenfenster konnte nicht erstellt werden — globale Hotkeys sind inaktiv.")]
    public static partial void WindowCreationFailed(ILogger logger);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Warning, Message = "Hotkey fuer {Action} konnte nicht registriert werden (vermutlich von einer anderen App belegt).")]
    public static partial void RegistrationFailed(ILogger logger, string action);
}
