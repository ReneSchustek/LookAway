using BenchmarkDotNet.Attributes;
using LookAway.Core.ValueObjects;
using LookAway.Data.Services;
using LookAway.MeasurementData;
using Microsoft.Extensions.Logging.Abstractions;

namespace LookAway.Benchmarks;

/// <summary>
/// Was vor jedem Pausenbeginn mit eigenem Bild geschieht: das Foto probeweise lesen (Größe und
/// mittlere Farbe). Solange das dauert, wartet die Pause.
/// </summary>
[MemoryDiagnoser]
public class OverlayImageBenchmarks : TemporaryDirectoryBenchmark
{
    private string _photo = string.Empty;
    private (int Width, int Height) _size;
    private readonly WindowsOverlayImageInspector _inspector = new(NullLogger<WindowsOverlayImageInspector>.Instance);

    /// <summary>Größe des Fotos.</summary>
    [Params("12 MP", "50 MP")]
    public string Photo { get; set; } = "12 MP";

    /// <summary>Legt das Foto an und prüft, dass es gelesen wird.</summary>
    /// <returns>Der Vorgang.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _photo = Path.Combine(CreateDirectory("photo"), "foto.jpg");
        _size = Photo == "50 MP" ? PhotoFile.FiftyMegapixels : PhotoFile.TwelveMegapixels;
        await PhotoFile.WriteJpegAsync(_photo, _size.Width, _size.Height).ConfigureAwait(false);

        OverlayImageInfo? info = await _inspector.InspectAsync(_photo).ConfigureAwait(false);
        if (info is null || info.PixelWidth != _size.Width)
        {
            throw new InvalidOperationException("Messbestand nicht gelesen: Das Foto ließ sich nicht prüfen.");
        }
    }


    /// <summary>Prüft das Foto wie vor dem Pausenbeginn.</summary>
    /// <returns>Breite des gelesenen Fotos.</returns>
    [Benchmark]
    public async Task<int> InspectPhoto() => (await _inspector.InspectAsync(_photo).ConfigureAwait(false))!.PixelWidth; // im Setup geprüft
}
