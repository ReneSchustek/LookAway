using System;
using System.Diagnostics.CodeAnalysis;
using LookAway.Core.Interfaces;
using LookAway.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace LookAway;

/// <summary>
/// Anwendungs-Bootstrap mit Dependency-Injection-Container.
/// Erzeugt den ServiceProvider beim Start und stellt ihn der App zur Verfuegung.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "WinUI-3-XAML-Compiler erfordert eine 'public partial'-App-Klasse fuer den generierten Activator.")]
public partial class App : Application
{
    /// <summary>
    /// Globaler Service-Provider, ueber den alle Schichten ihre Abhaengigkeiten beziehen.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;

    /// <summary>
    /// Initialisiert die Anwendung und den DI-Container.
    /// </summary>
    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    /// <summary>
    /// Wird beim Start der Anwendung aufgerufen und oeffnet das Hauptfenster.
    /// </summary>
    /// <param name="args">Vom System gelieferte Startparameter.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// Registriert alle Services im DI-Container.
    /// Wird beim Start einmalig aufgerufen.
    /// </summary>
    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        _ = services.AddLogging(builder =>
        {
            _ = builder.SetMinimumLevel(LogLevel.Information);
        });

        // Persistenz: Benutzerkonfiguration in %APPDATA%\LookAway\settings.json
        _ = services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();

        // Weitere Services werden hier registriert
        // (Timer, Tray, Localization, ...)

        return services.BuildServiceProvider();
    }
}
