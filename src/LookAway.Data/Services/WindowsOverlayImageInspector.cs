using System.Runtime.InteropServices;
using LookAway.Core.Domain;
using LookAway.Core.Interfaces;
using LookAway.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace LookAway.Data.Services;

/// <summary>
/// Liest ein Overlay-Bild mit dem Bilddecoder von Windows: Größe und mittlere Farbe. Was sich
/// nicht lesen lässt, endet mit <c>null</c> und einem Protokolleintrag, nie mit einer Ausnahme.
/// </summary>
public sealed class WindowsOverlayImageInspector : IOverlayImageInspector
{
    // Für den Mittelwert genügt ein grobes Raster: 32 × 32 Punkte mitteln auch ein feines Motiv
    // zuverlässig, und der Decoder rechnet das Bild dafür ohnehin nur verkleinert.
    private const uint SampleSize = 32;

    private const int BytesPerPixel = 4;

    private readonly ILogger<WindowsOverlayImageInspector> _logger;

    /// <summary>Erzeugt den Bildprüfer.</summary>
    /// <param name="logger">Protokolliert, warum ein Bild nicht taugt.</param>
    public WindowsOverlayImageInspector(ILogger<WindowsOverlayImageInspector> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OverlayImageInfo?> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!OverlayImage.IsSupported(path))
        {
            WindowsOverlayImageInspectorLog.UnsupportedFormat(_logger, path);
            return null;
        }

        try
        {
            return await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnreadable(ex))
        {
            WindowsOverlayImageInspectorLog.ImageUnreadable(_logger, ex, path);
            return null;
        }
    }

    // Datei fehlt oder ist gesperrt, keine Leserechte, ein unbrauchbarer Pfad, oder der Decoder
    // erkennt den Inhalt nicht (dann meldet Windows einen COM-Fehlercode). Ein Abbruch durch den
    // Aufrufer gehört nicht dazu und geht an ihn weiter.
    private static bool IsUnreadable(Exception exception)
        => exception is IOException or UnauthorizedAccessException or COMException or ArgumentException or NotSupportedException;

    private static async Task<OverlayImageInfo> ReadAsync(string path, CancellationToken cancellationToken)
    {
        FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        await using (file.ConfigureAwait(false))
        {
            using IRandomAccessStream stream = file.AsRandomAccessStream();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            byte[] sample = await SampleAsync(decoder, cancellationToken).ConfigureAwait(false);

            return new OverlayImageInfo((int)decoder.OrientedPixelWidth, (int)decoder.OrientedPixelHeight, Average(sample));
        }
    }

    // Das Bild verkleinert als BGRA-Pixel, in der Ausrichtung, in der es gezeigt wird.
    private static async Task<byte[]> SampleAsync(BitmapDecoder decoder, CancellationToken cancellationToken)
    {
        BitmapTransform sample = new()
        {
            ScaledWidth = SampleSize,
            ScaledHeight = SampleSize,
            // Nächster Nachbar lässt den JPEG-Decoder schon beim Dekodieren verkleinern: gemessen rund
            // ein Fünftel schneller als Fant (12 MP: 152 → 123 ms, 50 MP: 614 → 507 ms), und solange
            // wartet jeder Pausenbeginn. Für den Mittelwert über 32 × 32 Punkte genügt die einfache
            // Abtastung.
            InterpolationMode = BitmapInterpolationMode.NearestNeighbor,
        };

        // Alpha wird übergangen: Durchsichtige Stellen eines PNG zählen mit ihrer Farbe. Fotos,
        // für die das Overlay gedacht ist, haben keine.
        PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                sample,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);

        return pixels.DetachPixelData();
    }

    private static (byte R, byte G, byte B) Average(byte[] bgra)
    {
        long blue = 0;
        long green = 0;
        long red = 0;
        for (int i = 0; i < bgra.Length; i += BytesPerPixel)
        {
            blue += bgra[i];
            green += bgra[i + 1];
            red += bgra[i + 2];
        }

        long count = bgra.Length / BytesPerPixel;
        return ((byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }
}

/// <summary>
/// Source-generierte Logging-Methoden des Bildprüfers.
/// </summary>
internal static partial class WindowsOverlayImageInspectorLog
{
    [LoggerMessage(EventId = 1823, Level = LogLevel.Warning, Message = "Overlay-Bild {Path} hat kein unterstütztes Format (JPG oder PNG); das Overlay zeigt die Farbe.")]
    public static partial void UnsupportedFormat(ILogger logger, string path);

    [LoggerMessage(EventId = 1824, Level = LogLevel.Warning, Message = "Overlay-Bild {Path} ist nicht lesbar; das Overlay zeigt die Farbe.")]
    public static partial void ImageUnreadable(ILogger logger, Exception exception, string path);
}
