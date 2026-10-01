namespace Hakufu.Services;

/// <summary>Filtro de un selector de ficheros: nombre visible + extensiones sin punto.</summary>
public sealed record FileFilter(string Name, params string[] Extensions)
{
    public static readonly FileFilter Mangas   = new("Archivos de manga", "pdf", "cbr", "cbz");
    public static readonly FileFilter Images   = new("Imágenes", "png", "jpg", "jpeg", "bmp", "gif", "webp");
    public static readonly FileFilter Backup   = new("Copia de Hakufu", "zip");
    public static readonly FileFilter Png      = new("Imagen PNG", "png");
}

public interface IFilePickerService
{
    /// <summary>Rutas elegidas, o array vacío si se cancela.</summary>
    Task<string[]> PickFilesAsync(string title, FileFilter filter, bool multiSelect = true);

    /// <summary>Ruta donde guardar, o null si se cancela.</summary>
    Task<string?> SaveFileAsync(string title, string suggestedName, FileFilter filter);

    /// <summary>Abre una carpeta en el explorador del sistema (Explorer, Finder, gestor de Linux).</summary>
    void OpenFolder(string path);
}
