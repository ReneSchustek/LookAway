using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace LookAway;

/// <summary>
/// Anwendungs-Bootstrap mit Dependency-Injection-Container.
/// Erzeugt den ServiceProvider beim Start und stellt ihn der App zur Verfuegung.
/// </summary>
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
    private static IServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Weitere Services werden hier registriert
        // (Settings, Timer, Tray, Localization, ...)

        return services.BuildServiceProvider();
    }
}
