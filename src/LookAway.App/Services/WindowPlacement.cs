namespace LookAway.App.Services;

/// <summary>Wo ein Fenster fester Größe auf einem Monitor erscheint.</summary>
internal static class WindowPlacement
{
    /// <summary>
    /// Linke obere Ecke, an der ein Fenster mittig im Arbeitsbereich steht. Der Arbeitsbereich
    /// lässt die Taskleiste aus, das Fenster rutscht also nicht darunter.
    /// </summary>
    /// <param name="areaX">Linker Rand des Arbeitsbereichs.</param>
    /// <param name="areaY">Oberer Rand des Arbeitsbereichs.</param>
    /// <param name="areaWidth">Breite des Arbeitsbereichs.</param>
    /// <param name="areaHeight">Höhe des Arbeitsbereichs.</param>
    /// <param name="width">Breite des Fensters.</param>
    /// <param name="height">Höhe des Fensters.</param>
    /// <returns>Linke obere Ecke des Fensters in Bildschirmkoordinaten.</returns>
    public static (int Left, int Top) CenterIn(int areaX, int areaY, int areaWidth, int areaHeight, int width, int height)
        => (areaX + ((areaWidth - width) / 2), areaY + ((areaHeight - height) / 2));
}
