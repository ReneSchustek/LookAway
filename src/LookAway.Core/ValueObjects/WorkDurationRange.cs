namespace LookAway.Core.ValueObjects;

/// <summary>
/// Zulässiger Bereich der Arbeitsdauer eines Modells, das eine begrenzte
/// Benutzer-Anpassung erlaubt (z. B. <c>PhysicalCounter</c> 30–45 Minuten).
/// Vorgesehen als Grenzen für einen UI-Slider.
/// </summary>
/// <remarks>
/// Kleiner, unveränderlicher Wertebereich — als <c>readonly record struct</c>
/// modelliert, um Heap-Allokationen zu vermeiden.
/// </remarks>
public readonly record struct WorkDurationRange
{
    /// <summary>
    /// Erzeugt einen Bereich. <paramref name="min"/> muss positiv und
    /// &#8804; <paramref name="max"/> sein.
    /// </summary>
    /// <param name="min">Untere Grenze.</param>
    /// <param name="max">Obere Grenze.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> ist nicht positiv.</exception>
    /// <exception cref="ArgumentException"><paramref name="min"/> ist größer als <paramref name="max"/>.</exception>
    public WorkDurationRange(TimeSpan min, TimeSpan max)
    {
        if (min <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "Min muss positiv sein.");
        }

        if (min > max)
        {
            throw new ArgumentException($"Min ({min}) darf nicht größer als Max ({max}) sein.", nameof(min));
        }

        Min = min;
        Max = max;
    }

    /// <summary>Untere Grenze der Arbeitsdauer.</summary>
    public TimeSpan Min { get; }

    /// <summary>Obere Grenze der Arbeitsdauer.</summary>
    public TimeSpan Max { get; }
}
