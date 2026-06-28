using System;
using System.Diagnostics.CodeAnalysis;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace LookAway.Services;

/// <summary>
/// Spielt die eingebetteten Erinnerungstoene ueber einen wiederverwendeten
/// <see cref="MediaPlayer"/> ab. Die Lautstaerke wird pro Wiedergabe
/// gesetzt; Fehler (z. B. Audiogeraete-Wechsel) werden geschluckt.
/// </summary>
internal sealed class SoundService : ISoundService, IDisposable
{
    private const double VolumeDivisor = 100.0;

    private readonly ILogger<SoundService> _logger;
    private readonly MediaPlayer _player = new();
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Service.
    /// </summary>
    /// <param name="logger">Logger fuer Wiedergabefehler.</param>
    public SoundService(ILogger<SoundService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Audiowiedergabe ist unkritisch: jeder Fehler (fehlendes Geraet, Geraetewechsel, COM-Fehler) wird geloggt und geschluckt, damit die App nie abstuerzt.")]
    public void Play(SoundType soundType, int volumePercent)
    {
        if (_disposed)
        {
            return;
        }

        int clamped = Math.Clamp(volumePercent, 0, 100);
        if (clamped == 0)
        {
            return;
        }

        try
        {
            Uri uri = new($"ms-appx:///Assets/Sounds/{GetFileName(soundType)}");
            _player.Source = MediaSource.CreateFromUri(uri);
            _player.Volume = clamped / VolumeDivisor;
            _player.Play();
        }
        catch (Exception ex)
        {
            // Audiowiedergabe darf die App nie zum Absturz bringen
            // (z. B. fehlendes Geraet oder Geraetewechsel zur Laufzeit).
            SoundServiceLog.PlaybackFailed(_logger, ex);
        }
    }

    private static string GetFileName(SoundType soundType) => soundType switch
    {
        SoundType.Chime => "chime.wav",
        SoundType.Bell => "bell.wav",
        SoundType.Pop => "pop.wav",
        _ => "chime.wav",
    };

    /// <summary>Gibt den MediaPlayer frei.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player.Dispose();
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Sound-Service.
/// </summary>
internal static partial class SoundServiceLog
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Warning,
        Message = "Erinnerungston konnte nicht abgespielt werden.")]
    public static partial void PlaybackFailed(ILogger logger, Exception exception);
}
