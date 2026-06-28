namespace LookAway.Core.Enums;

/// <summary>
/// Modifikatortasten eines globalen Hotkeys. Die Werte entsprechen den Win32-
/// <c>MOD_*</c>-Konstanten, damit sie direkt an <c>RegisterHotKey</c> uebergeben
/// werden koennen.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>Kein Modifikator.</summary>
    None = 0,

    /// <summary>Alt (<c>MOD_ALT</c>).</summary>
    Alt = 0x0001,

    /// <summary>Strg (<c>MOD_CONTROL</c>).</summary>
    Control = 0x0002,

    /// <summary>Umschalt (<c>MOD_SHIFT</c>).</summary>
    Shift = 0x0004,

    /// <summary>Windows-Taste (<c>MOD_WIN</c>).</summary>
    Win = 0x0008,
}
