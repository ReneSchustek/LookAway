using LookAway.Core.ValueObjects;
using LookAway.Data.Services;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace LookAway.Data.Tests;

/// <summary>
/// Der Bildprüfer am echten Windows-Bilddecoder: Größe und mittlere Farbe eines lesbaren Bilds, und
/// für fehlende, kaputte oder fremde Dateien <c>null</c> mit einem Eintrag im Protokoll.
/// </summary>
public sealed class WindowsOverlayImageInspectorTests : IDisposable
{
    private const int UnsupportedFormatEvent = 1823;
    private const int UnreadableEvent = 1824;

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "lookaway-overlay-image", TestIds.Next().ToString("N"));

    private readonly RecordingLogger<WindowsOverlayImageInspector> _logger = new();
    private readonly WindowsOverlayImageInspector _inspector;

    public WindowsOverlayImageInspectorTests()
    {
        _ = Directory.CreateDirectory(_directory);
        _inspector = new WindowsOverlayImageInspector(_logger);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Inspect_ReadsSizeAndAverageColorOfAPng()
    {
        string path = await WriteImageAsync("rot.png", BitmapEncoder.PngEncoderId, width: 6, height: 4, (_, _) => (200, 40, 10));

        OverlayImageInfo? info = await _inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.Equal(6, info.PixelWidth);
        Assert.Equal(4, info.PixelHeight);
        Assert.Equal(((byte)200, (byte)40, (byte)10), info.AverageColor);
        Assert.Empty(_logger.Entries);
    }

    /// <remarks>
    /// Halb schwarz, halb weiß ergibt im Mittel ein Grau um 128. Der Decoder rechnet zum
    /// Verkleinern zwischen den Pixeln, deshalb eine kleine Toleranz statt eines exakten Werts.
    /// </remarks>
    [Fact]
    public async Task Inspect_AveragesOverTheWholeImage()
    {
        string path = await WriteImageAsync("halb.png", BitmapEncoder.PngEncoderId, width: 64, height: 64, (x, _) => x < 32 ? (byte)0 : (byte)255);

        OverlayImageInfo? info = await _inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.InRange(info.AverageColor.R, 120, 136);
        Assert.Equal(info.AverageColor.R, info.AverageColor.G);
        Assert.Equal(info.AverageColor.R, info.AverageColor.B);
    }

    [Fact]
    public async Task Inspect_ReadsAJpeg()
    {
        string path = await WriteImageAsync("grau.jpg", BitmapEncoder.JpegEncoderId, width: 16, height: 8, (_, _) => (128, 128, 128));

        OverlayImageInfo? info = await _inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.Equal(16, info.PixelWidth);
        Assert.InRange(info.AverageColor.G, 124, 132);
    }

    [Fact]
    public async Task Inspect_WithAMissingFile_ReturnsNullAndLogs()
    {
        OverlayImageInfo? info = await _inspector.InspectAsync(Path.Combine(_directory, "fehlt.jpg"), TestContext.Current.CancellationToken);

        Assert.Null(info);
        Assert.Equal((LogLevel.Warning, UnreadableEvent), Assert.Single(_logger.Entries));
    }

    [Fact]
    public async Task Inspect_WithABrokenFile_ReturnsNullAndLogs()
    {
        string path = Path.Combine(_directory, "kaputt.png");
        await File.WriteAllTextAsync(path, "kein Bild, nur Text", TestContext.Current.CancellationToken);

        OverlayImageInfo? info = await _inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Null(info);
        Assert.Equal((LogLevel.Warning, UnreadableEvent), Assert.Single(_logger.Entries));
    }

    [Fact]
    public async Task Inspect_WithAnUnsupportedFormat_ReturnsNullWithoutOpeningTheFile()
    {
        OverlayImageInfo? info = await _inspector.InspectAsync(Path.Combine(_directory, "bild.gif"), TestContext.Current.CancellationToken);

        Assert.Null(info);
        Assert.Equal((LogLevel.Warning, UnsupportedFormatEvent), Assert.Single(_logger.Entries));
    }

    [Fact]
    public async Task Inspect_WithoutPath_Throws()
        => _ = await Assert.ThrowsAsync<ArgumentNullException>(() => _inspector.InspectAsync(null!, TestContext.Current.CancellationToken));

    [Fact]
    public void Constructor_WithoutLogger_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WindowsOverlayImageInspector(null!));

    private Task<string> WriteImageAsync(string name, Guid encoderId, int width, int height, Func<int, int, byte> gray)
        => WriteImageAsync(name, encoderId, width, height, (x, y) => (gray(x, y), gray(x, y), gray(x, y)));

    private async Task<string> WriteImageAsync(string name, Guid encoderId, int width, int height, Func<int, int, (byte R, byte G, byte B)> color)
    {
        byte[] bgra = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (byte r, byte g, byte b) = color(x, y);
                int i = ((y * width) + x) * 4;
                (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]) = (b, g, r, 255);
            }
        }

        string path = Path.Combine(_directory, name);
        using (InMemoryRandomAccessStream stream = new())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, bgra);
            await encoder.FlushAsync();

            byte[] encoded = new byte[stream.Size];
            using DataReader reader = new(stream.GetInputStreamAt(0));
            _ = await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(encoded);
            await File.WriteAllBytesAsync(path, encoded, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        return path;
    }
}
