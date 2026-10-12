using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Hakufu.Services;

/// <summary>
/// Orden de las páginas de un CBZ/CBR: natural, como los tomos y las colecciones
/// ("2.jpg" antes que "10.jpg"). Hasta la 0.12 era ordinal ("10.jpg" antes que "2.jpg"),
/// y con nombres sin ceros delante las páginas salían desordenadas.
/// </summary>
public static class PageOrder
{
    /// <summary>Las imágenes del archivo, que son sus páginas, en orden natural.</summary>
    internal static List<string> ImageKeys(IArchive archive) =>
        archive.Entries
            .Where(e => !e.IsDirectory &&
                        CoverService.ImageExtensions.Contains(Path.GetExtension(e.Key ?? "").ToLowerInvariant()))
            .Select(e => e.Key!)
            .OrderBy(k => k, NaturalComparer.Instance)
            .ToList();

    /// <summary>
    /// La página del orden natural que es la <paramref name="oldIndex"/> del orden ordinal de
    /// hasta la 0.12 (para pasar el progreso guardado), o null si no se puede leer el archivo.
    /// </summary>
    public static int? FromOrdinal(string filePath, int oldIndex)
    {
        // En un PDF el orden lo pone el PDF: no cambia nada.
        if (Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return oldIndex;
        try
        {
            if (!File.Exists(filePath)) return null;
            using var archive = ArchiveFactory.OpenArchive(filePath, new ReaderOptions());
            return FromOrdinal(ImageKeys(archive), oldIndex);
        }
        catch
        {
            return null;
        }
    }

    /// <param name="pages">Las páginas en orden natural.</param>
    internal static int FromOrdinal(IReadOnlyList<string> pages, int oldIndex)
    {
        if (pages.Count == 0) return oldIndex;
        // Terminado sigue terminado: en el orden viejo ya se habían pasado todas las páginas.
        if (oldIndex >= pages.Count - 1) return pages.Count - 1;
        if (oldIndex <= 0) return 0;
        var name = pages.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ElementAt(oldIndex);
        return Math.Max(0, pages.ToList().IndexOf(name));
    }
}
