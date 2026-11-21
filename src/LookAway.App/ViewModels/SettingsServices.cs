using LookAway.App.Services;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;

namespace LookAway.App.ViewModels;

/// <summary>Die Dienste, mit denen das Einstellungsfenster lädt, speichert und Vorschau gibt.</summary>
/// <param name="Repository">Persistenz der Einstellungen.</param>
/// <param name="AutoStart">Hält Einstellung und Registry synchron.</param>
/// <param name="Localization">Liefert Texte und steuert den Sprachwechsel.</param>
/// <param name="Sound">Spielt den Erinnerungston für die Vorschau.</param>
/// <param name="UpdateChecker">Prüft auf Updates.</param>
/// <param name="UpdateInstaller">Lädt und stellt ein gefundenes Update bereit (Ein-Klick-Installation).</param>
/// <param name="SetupUpdate">Aktualisiert eine installierte Kopie über das signierte Setup.</param>
internal sealed record SettingsServices(
    ISettingsRepository Repository,
    AutoStartCoordinator AutoStart,
    ILocalizationService Localization,
    ISoundService Sound,
    IUpdateChecker UpdateChecker,
    IUpdateInstaller UpdateInstaller,
    ISetupUpdate SetupUpdate);
