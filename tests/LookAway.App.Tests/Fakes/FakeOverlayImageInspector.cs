using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;

namespace LookAway.App.Tests.Fakes;

/// <summary>
/// Bildprüfer ohne Datei: liefert ein vorgegebenes Ergebnis, wirft auf Wunsch oder hält die
/// Antwort zurück, bis der Test sie freigibt.
/// </summary>
internal sealed class FakeOverlayImageInspector : IOverlayImageInspector
{
    private TaskCompletionSource<OverlayImageInfo?>? _pending;

    /// <summary>Was ein sofort beantwortetes Prüfen liefert; <c>null</c> heißt „nicht lesbar“.</summary>
    public OverlayImageInfo? Result { get; set; }

    /// <summary>Wird beim Prüfen geworfen, wenn gesetzt.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Der zuletzt geprüfte Pfad.</summary>
    public string? InspectedPath { get; private set; }

    /// <summary>Die nächsten Prüfungen warten, bis <see cref="Complete"/> sie beantwortet.</summary>
    /// <remarks>
    /// Ohne asynchrone Fortsetzungen: <see cref="Complete"/> führt den wartenden Ablauf gleich zu
    /// Ende, und der Test prüft danach ohne Warten.
    /// </remarks>
    public void HoldAnswers() => _pending = new TaskCompletionSource<OverlayImageInfo?>();

    /// <summary>Beantwortet eine zurückgehaltene Prüfung.</summary>
    /// <param name="info">Das Ergebnis.</param>
    public void Complete(OverlayImageInfo? info) => _pending!.SetResult(info); // nur nach HoldAnswers

    public Task<OverlayImageInfo?> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        InspectedPath = path;

        if (Failure is not null)
        {
            return Task.FromException<OverlayImageInfo?>(Failure);
        }

        return _pending?.Task ?? Task.FromResult(Result);
    }
}
