using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LookAway.Core.Enums;
using LookAway.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace LookAway.App.Services;

/// <summary>
/// Spielt die eingebetteten Erinnerungstöne über einen wiederverwendeten
/// <see cref="MediaPlayer"/> ab. Die Lautstärke wird pro Wiedergabe
/// gesetzt; Fehler (z. B. Audiogeräte-Wechsel) werden geschluckt.
/// </summary>
internal sealed class SoundService : ISoundService, IDisposable
{
    // Die Einstellung zählt in Prozent, MediaPlayer.Volume von 0 bis 1.
    private const double VolumeDivisor = 100.0;

    private readonly ILogger<SoundService> _logger;
    private readonly MediaPlayer _player = new();
    private MediaSource? _currentSource;
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Service.
    /// </summary>
    /// <param name="logger">Logger für Wiedergabefehler.</param>
    public SoundService(ILogger<SoundService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
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

        PlayFile(GetFileName(soundType), clamped / VolumeDivisor);
    }

    // Spielt eine Datei aus den Paket-Assets ab. Fehler gehen ins Protokoll, nie an den Aufrufer.
    [ExcludeFromCodeCoverage(Justification = "Spielt über den WinRT-MediaPlayer auf dem echten Audiogerät ab; Wächter und Dateiwahl davor prüft SoundServiceTests.")]
    private void PlayFile(string fileName, double volume)
    {
        try
        {
            Uri uri = new($"ms-appx:///Assets/Sounds/{fileName}");
            MediaSource source = MediaSource.CreateFromUri(uri);
            _player.Source = source;

            // Vorherige Quelle erst nach dem Umhängen freigeben (kein COM-Leak).
            _currentSource?.Dispose();
            _currentSource = source;

            _player.Volume = volume;
            _player.Play();
        }
        // Der WinRT-MediaPlayer meldet fehlende oder gewechselte Audiogeräte über
        // COM-Fehler; ein bereits freigegebener Player wirft ObjectDisposedException.
        catch (Exception ex) when (ex is COMException or ObjectDisposedException or InvalidOperationException)
        {
            SoundServiceLog.PlaybackFailed(_logger, ex);
        }
    }

    /// <summary>Datei des Erinnerungstons unter <c>Assets/Sounds</c>.</summary>
    /// <param name="soundType">Gewählter Ton.</param>
    /// <returns>Dateiname; ein unbekannter Ton fällt auf den Gong zurück.</returns>
    internal static string GetFileName(SoundType soundType) => soundType switch
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
        _currentSource?.Dispose();
        _currentSource = null;
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Sound-Service.
/// </summary>
internal static partial class SoundServiceLog
{
    [LoggerMessage(
        EventId = 1720,
        Level = LogLevel.Warning,
        Message = "Erinnerungston konnte nicht abgespielt werden.")]
    public static partial void PlaybackFailed(ILogger logger, Exception exception);
}
