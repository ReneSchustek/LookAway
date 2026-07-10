using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.Services;
using LookAway.Core.Enums;

namespace LookAway.Core.Tests.Fakes;

/// <summary>Test-Fake für <see cref="IReminderPresenter"/>.</summary>
internal sealed class FakeReminderPresenter : IReminderPresenter
{
    public bool IsReminderOpen { get; set; }

    public int ShowCount { get; private set; }

    public BreakModel? LastModel { get; private set; }

    public TimeSpan? LastAutoStartAfter { get; private set; }

    private Action<ReminderResult>? _onResult;

    public void Show(BreakModel model, TimeSpan? autoStartAfter, Action<ReminderResult> onResult)
    {
        ShowCount++;
        LastModel = model;
        LastAutoStartAfter = autoStartAfter;
        _onResult = onResult;
    }

    /// <summary>Simuliert die Benutzerentscheidung im Erinnerungsfenster.</summary>
    public void CompleteWith(ReminderResult result) => _onResult?.Invoke(result);
}

/// <summary>Test-Fake für <see cref="IBreakOverlayPresenter"/>.</summary>
internal sealed class FakeBreakOverlayPresenter : IBreakOverlayPresenter
{
    public bool IsOverlayOpen { get; private set; }

    public int ShowCount { get; private set; }

    public string? LastColor { get; private set; }

    public bool LastDarkenAllScreens { get; private set; }

    private Action<BreakEndReason>? _onEnded;

    public void Show(BreakModel model, TimeSpan breakDuration, string overlayColorHex, bool darkenAllScreens, Action<BreakEndReason> onEnded)
    {
        ShowCount++;
        LastColor = overlayColorHex;
        LastDarkenAllScreens = darkenAllScreens;
        _onEnded = onEnded;
        IsOverlayOpen = true;
    }

    public void Close() => IsOverlayOpen = false;

    /// <summary>Simuliert das Ende der Pause aus dem Overlay.</summary>
    public void EndWith(BreakEndReason reason)
    {
        IsOverlayOpen = false;
        _onEnded?.Invoke(reason);
    }
}

/// <summary>Test-Fake für <see cref="ITrayController"/>.</summary>
internal sealed class FakeTrayController : ITrayController
{
    public BreakModel? ActiveModel { get; private set; }

    public bool DndActive { get; private set; }

    public void SetActiveModel(BreakModel model) => ActiveModel = model;

    public void SetDndActive(bool isDndActive) => DndActive = isDndActive;
}
