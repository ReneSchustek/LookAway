using LookAway.Application.Localization;
using LookAway.Core.Enums;
using LookAway.Core.ValueObjects;
using LookAway.Tests.Unit.Fakes;

namespace LookAway.Tests.Unit.Application.Localization;

/// <summary>
/// Tests für <see cref="HotkeyTextKeys.Format"/>: die Modifikatornamen werden
/// sprachabhängig über die <see cref="Core.Interfaces.ILocalizationService"/>
/// aufgelöst, die Taste bleibt sprachneutral.
/// </summary>
public sealed class HotkeyTextKeysTests
{
    private const int VkP = 0x50;

    [Fact]
    public void Format_rendert_lokalisierte_Modifikatornamen()
    {
        Dictionary<string, string> english = new()
        {
            [HotkeyTextKeys.ModifierControl] = "Ctrl",
            [HotkeyTextKeys.ModifierAlt] = "Alt",
        };
        FakeLocalizationService localization = new(Language.English, english);
        HotkeyDefinition definition = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkP);

        Assert.Equal("Ctrl+Alt+P", HotkeyTextKeys.Format(definition, localization));
    }

    [Fact]
    public void Format_nutzt_deutsche_Modifikatornamen()
    {
        Dictionary<string, string> german = new()
        {
            [HotkeyTextKeys.ModifierControl] = "Strg",
            [HotkeyTextKeys.ModifierShift] = "Umschalt",
        };
        FakeLocalizationService localization = new(Language.German, german);
        HotkeyDefinition definition = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkP);

        Assert.Equal("Strg+Umschalt+P", HotkeyTextKeys.Format(definition, localization));
    }
}
