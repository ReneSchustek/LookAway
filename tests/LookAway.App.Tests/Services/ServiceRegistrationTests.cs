using System.Net.Http;
using LookAway.App.Services;
using LookAway.App.Tests.Fakes;
using LookAway.Core.Interfaces;
using LookAway.Data.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LookAway.App.Tests.Services;

/// <summary>
/// Sichert den Container ab: Jede angemeldete Dienstart lässt sich auflösen, und der
/// Netzwerk-Client ist ein einziger <see cref="HttpGetClient"/>, dessen Client aus der Factory
/// kommt und dessen Handler die Verbindungen selbst erneuert.
/// </summary>
public sealed class ServiceRegistrationTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "lookaway-container-tests", TestIds.Next().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    /// <remarks>
    /// Eine vergessene oder falsch verdrahtete Anmeldung fiele sonst erst beim Programmstart auf,
    /// als Absturz vor dem ersten Fenster. Aufgelöst wird jede Dienstart, die der Container kennt.
    /// </remarks>
    [Fact]
    public void Container_ResolvesEveryRegisteredService()
    {
        ServiceCollection services = new();
        ServiceRegistration.Register(services, _dataDirectory, new Version(1, 2, 3), debugLogging: false);
        List<string> failures = [];

        using (ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }))
        {
            foreach (Type serviceType in services.Select(descriptor => descriptor.ServiceType).Where(type => !type.IsGenericTypeDefinition).Distinct())
            {
                try
                {
                    _ = provider.GetRequiredService(serviceType);
                }
                catch (Exception ex) when (ex is InvalidOperationException or TypeInitializationException or System.Runtime.InteropServices.COMException)
                {
                    failures.Add($"{serviceType.Name}: {ex.Message}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Build_ReturnsAProviderWithTheLoggingOfTheDataDirectory()
    {
        using ServiceProvider provider = ServiceRegistration.Build(_dataDirectory, new Version(1, 2, 3), debugLogging: true);

        ILogger<ServiceRegistrationTests> logger = provider.GetRequiredService<ILogger<ServiceRegistrationTests>>();

        Assert.True(logger.IsEnabled(LogLevel.Debug));
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();
        ServiceRegistration.AddHttpGetClient(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void HttpGetClient_IsRegisteredOnce()
    {
        using ServiceProvider provider = BuildProvider();

        IHttpGetClient first = provider.GetRequiredService<IHttpGetClient>();

        _ = Assert.IsType<HttpGetClient>(first);
        Assert.Same(first, provider.GetRequiredService<IHttpGetClient>());
    }

    [Fact]
    public void FactoryClient_UsesTheHandlerWithLimitedConnectionLifetime()
    {
        using ServiceProvider provider = BuildProvider();

        HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(HttpGetClient.ClientName);
        while (handler is DelegatingHandler { InnerHandler: { } inner })
        {
            handler = inner;
        }

        SocketsHttpHandler primary = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.Equal(TimeSpan.FromMinutes(15), primary.PooledConnectionLifetime);
    }

    [Fact]
    public void FactoryClient_KeepsItsHandlerForTheWholeRun()
    {
        using ServiceProvider provider = BuildProvider();

        HttpClientFactoryOptions options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>()
            .Get(HttpGetClient.ClientName);

        Assert.Equal(Timeout.InfiniteTimeSpan, options.HandlerLifetime);
    }
}
