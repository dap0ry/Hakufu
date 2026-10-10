using Hakufu.I18n;

namespace Hakufu.Services;

/// <summary>Filtro de un selector de ficheros: nombre visible + extensiones sin punto.</summary>
public sealed record FileFilter(string Name, params string[] Extensions)
{
    // Propiedades (no campos): el nombre sale en el idioma de cada momento.
    public static FileFilter Mangas => new(L.Get("files.mangas"), "pdf", "cbr", "cbz");
    public static FileFilter Images => new(L.Get("files.images"), "png", "jpg", "jpeg", "bmp", "gif", "webp");
    public static FileFilter Backup => new(L.Get("files.backup"), "zip");
    public static FileFilter Png    => new(L.Get("files.png"), "png");
}

public interface IFilePickerService
{
    /// <summary>Rutas elegidas, o array vacío si se cancela.</summary>
    Task<string[]> PickFilesAsync(string title, FileFilter filter, bool multiSelect = true);

    /// <summary>Una imagen (en iOS, de Fotos), o null si se cancela.</summary>
    Task<string?> PickImageAsync(string title);

    /// <summary>Carpeta elegida, o null si se cancela. FolderNotOnDeviceException si no está en el dispositivo (Android).</summary>
    Task<string?> PickFolderAsync(string title);

    /// <summary>Ruta donde guardar, o null si se cancela.</summary>
    Task<string?> SaveFileAsync(string title, string suggestedName, FileFilter filter);

    /// <summary>Abre una carpeta en el explorador del sistema (Explorer, Finder, gestor de Linux).</summary>
    void OpenFolder(string path);
}
