using System.Runtime.InteropServices;
using LookAway.Core.Domain;
using LookAway.Core.Enums;
using LookAway.Core.Events;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LookAway.Data.Services;

/// <summary>
/// Registriert globale Hotkeys über die Win32-API <c>RegisterHotKey</c>.
/// Ein eigener Hintergrund-Thread betreibt die Nachrichtenschleife und empfängt
/// <c>WM_HOTKEY</c>.
/// </summary>
/// <remarks>
/// Ohne Fensterhandle stellt Windows <c>WM_HOTKEY</c> direkt in die Nachrichten-
/// warteschlange des registrierenden Threads. Der Dienst hält deshalb kein natives
/// Handle; Registrierung und Freigabe sind thread-affin und werden über
/// <c>PostThreadMessage</c> auf den Nachrichten-Thread marshalled.
/// </remarks>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed partial class WindowsHotkeyService : IHotkeyService, IDisposable
{
    private const uint WmHotkey = 0x0312;
    private const uint WmApplyBindings = 0x0400; // WM_APP
    private const uint WmQuitLoop = 0x0401;      // WM_APP + 1 — beendet den Message-Loop
    private const uint ModNoRepeat = 0x4000;
    private const uint PmNoRemove = 0x0000;

    // Ohne Fenster: RegisterHotKey/UnregisterHotKey adressieren den Thread selbst.
    private const nint NoWindow = 0;

    private readonly ILogger<WindowsHotkeyService> _logger;
    private readonly Lock _gate = new();
    private readonly List<HotkeyAction> _registered = new();
    private readonly ManualResetEventSlim _queueReady = new(initialState: false);

    private Dictionary<HotkeyAction, HotkeyDefinition> _pending = new();
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _disposed;

    /// <summary>
    /// Erzeugt den Service und startet den Nachrichten-Thread.
    /// </summary>
    /// <param name="logger">Logger für Registrierungs-Vorgänge.</param>
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

    private void SignalApply() => PostToMessageThread(WmApplyBindings);

    // Eine Thread-Nachricht geht verloren, solange der Thread noch keine Warteschlange
    // besitzt. Deshalb erst auf deren Erzeugung warten (der Thread signalisiert sie).
    private void PostToMessageThread(uint message)
    {
        if (!_queueReady.Wait(TimeSpan.FromSeconds(2)))
        {
            return;
        }

        uint threadId = Volatile.Read(ref _threadId);
        if (threadId != 0)
        {
            _ = PostThreadMessageW(threadId, message, 0, 0);
        }
    }

    private void RunMessageLoop()
    {
        Volatile.Write(ref _threadId, GetCurrentThreadId());

        // Erzwingt die Erzeugung der Nachrichtenwarteschlange, bevor der erste
        // PostThreadMessage-Aufruf eintreffen kann.
        _ = PeekMessageW(out _, NoWindow, 0, 0, PmNoRemove);
        _queueReady.Set();

        // Falls vor dem Start des Loops bereits Bindings gesetzt wurden.
        ApplyPending();

        while (true)
        {
            int result = GetMessageW(out Msg message, NoWindow, 0, 0);
            if (result == 0)
            {
                // WM_QUIT — regulärer Abbau (falls nicht bereits über WmQuitLoop erfolgt).
                break;
            }

            if (result == -1)
            {
                // Fehler in der Nachrichtenschleife: Hotkeys freigeben, damit sie nicht
                // bis Prozessende global belegt bleiben, dann geordnet beenden.
                WindowsHotkeyServiceLog.MessageLoopFailed(_logger);
                break;
            }

            switch (message.Message)
            {
                case WmHotkey:
                    OnHotkey((int)message.WParam);
                    break;
                case WmApplyBindings:
                    ApplyPending();
                    break;
                case WmQuitLoop:
                    UnregisterAllOnMessageThread();
                    return;
                default:
                    break;
            }
        }

        UnregisterAllOnMessageThread();
    }

    private void UnregisterAllOnMessageThread()
    {
        foreach (HotkeyAction action in _registered)
        {
            _ = UnregisterHotKey(NoWindow, (int)action);
        }

        _registered.Clear();
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
        UnregisterAllOnMessageThread();

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
            if (RegisterHotKey(NoWindow, (int)action, modifiers, (uint)definition.VirtualKey))
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

        // Abbau auf dem Nachrichten-Thread anstossen (Hotkeys sind thread-affin).
        PostToMessageThread(WmQuitLoop);

        _ = _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _queueReady.Dispose();
    }

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

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetMessageW(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessageW(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessageW(uint threadId, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint GetCurrentThreadId();
}

/// <summary>
/// Source-generierte Logging-Methoden des Hotkey-Service.
/// </summary>
internal static partial class WindowsHotkeyServiceLog
{
    [LoggerMessage(EventId = 1501, Level = LogLevel.Warning, Message = "Hotkey für {Action} konnte nicht registriert werden (vermutlich von einer anderen App belegt).")]
    public static partial void RegistrationFailed(ILogger logger, string action);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Warning, Message = "Hotkey-Nachrichtenschleife mit Fehler beendet — globale Hotkeys sind bis zum Neustart inaktiv.")]
    public static partial void MessageLoopFailed(ILogger logger);
}
