using System.Diagnostics.CodeAnalysis;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace LookAway.MeasurementData;

/// <summary>
/// Ein JPEG in Fotogröße, aus einem festen Startwert erzeugt: Verläufe mit Rauschen, damit der
/// Decoder so viel zu tun hat wie bei einem echten Foto. Eine einfarbige Fläche komprimiert zu fast
/// nichts und wäre eine Messung über gar nichts.
/// </summary>
[SuppressMessage("Security", "CA5394", Justification = "Fester Startwert für einen reproduzierbaren Messbestand; die Zahlen haben keinen Sicherheitsbezug.")]
internal static class PhotoFile
{
    /// <summary>Startwert des Rauschens.</summary>
    public const int Seed = 20261123;

    /// <summary>Ein übliches Handyfoto: 12 Megapixel.</summary>
    public static (int Width, int Height) TwelveMegapixels { get; } = (4000, 3000);

    /// <summary>Ein großes Kamera- oder Handyfoto: 50 Megapixel.</summary>
    public static (int Width, int Height) FiftyMegapixels { get; } = (8160, 6120);

    /// <summary>Schreibt das Foto als JPEG.</summary>
    /// <param name="path">Zieldatei.</param>
    /// <param name="width">Breite in Pixeln.</param>
    /// <param name="height">Höhe in Pixeln.</param>
    /// <returns>Der Vorgang.</returns>
    public static async Task WriteJpegAsync(string path, int width, int height)
    {
        byte[] bgra = Pixels(width, height);

        using InMemoryRandomAccessStream stream = new();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, bgra);
        await encoder.FlushAsync();

        byte[] encoded = new byte[stream.Size];
        using DataReader reader = new(stream.GetInputStreamAt(0));
        _ = await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(encoded);
        await File.WriteAllBytesAsync(path, encoded).ConfigureAwait(false);
    }

    private static byte[] Pixels(int width, int height)
    {
        Random random = new(Seed);
        byte[] bgra = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            int row = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int noise = random.Next(-24, 25);
                int i = row + (x * 4);
                bgra[i] = Clamp((x * 255 / width) + noise);
                bgra[i + 1] = Clamp((y * 255 / height) + noise);
                bgra[i + 2] = Clamp(128 + noise);
                bgra[i + 3] = 255;
            }
        }

        return bgra;
    }

    private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);
}
