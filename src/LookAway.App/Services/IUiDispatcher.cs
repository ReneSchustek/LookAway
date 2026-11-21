
namespace LookAway.App.Services;

/// <summary>Wechselt auf den Fenster-Thread der Anwendung.</summary>
/// <remarks>
/// Fenster dürfen nur auf diesem Thread entstehen. Die Presenter werden aber auch vom
/// Timer-Thread aufgerufen; sie reichen ihre Arbeit deshalb hierüber weiter.
/// </remarks>
internal interface IUiDispatcher
{
    /// <summary>Führt <paramref name="action"/> auf dem Fenster-Thread aus.</summary>
    /// <param name="action">Auszuführende Aktion.</param>
    void Post(Action action);
}
