using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Hakufu.Data;
using Hakufu.I18n;

namespace Hakufu.Services;

/// <summary>La carpeta elegida no está en el móvil (Drive, una carpeta virtual…): no tiene ruta.</summary>
public sealed class FolderNotOnDeviceException() : Exception("La carpeta elegida no está en el almacenamiento del dispositivo.");

public class FilePickerService : IFilePickerService
{
    // La ventana en escritorio; en iOS, la vista única.
    private static IStorageProvider? Storage => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow?.StorageProvider,
        ISingleViewApplicationLifetime single => TopLevel.GetTopLevel(single.MainView)?.StorageProvider,
        _ => null
    };

    // iOS: lo elegido fuera de la app solo se puede leer con permiso temporal; se
    // copia aquí y los servicios trabajan con la copia.
    internal static string PickedDir => Path.Combine(Path.GetTempPath(), "Hakufu-picked");

    // Los selectores de GTK (Linux) distinguen mayúsculas en los patrones:
    // sin "*.CBZ" no se verían "Tomo 01.CBZ" ni "foto.JPG".
    private static FilePickerFileType ToFileType(FileFilter f) => new(f.Name)
    {
        Patterns = f.Extensions
            .SelectMany(e => new[] { $"*.{e.ToLowerInvariant()}", $"*.{e.ToUpperInvariant()}" })
            .ToArray()
    };

    public async Task<string[]> PickFilesAsync(string title, FileFilter filter, bool multiSelect = true)
    {
        if (Storage is not { } storage) return [];
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title          = title,
            AllowMultiple  = multiSelect,
            FileTypeFilter = [ToFileType(filter)]
        });
        if (AppPlatform.IsMobile)
        {
            var copies = new List<string>();
            foreach (var f in files)
            {
                try { copies.Add(await CopyToLocalAsync(f.Name, f.OpenReadAsync, PickedDir)); }
                catch { /* no se pudo leer: como si no se hubiera elegido */ }
            }
            return copies.ToArray();
        }
        return files.Select(f => f.TryGetLocalPath())
                    .OfType<string>()
                    .ToArray();
    }

    public async Task<string?> PickImageAsync(string title)
    {
        if (AppPlatform.PickPhotoAsync is { } pickPhoto) return await pickPhoto(); // iOS: Fotos
        var files = await PickFilesAsync(title, FileFilter.Images, multiSelect: false);
        return files.FirstOrDefault();
    }

    /// <summary>Copia un archivo que solo se puede abrir como stream a <paramref name="dir"/>.</summary>
    internal static async Task<string> CopyToLocalAsync(string name, Func<Task<Stream>> openRead, string dir)
    {
        Directory.CreateDirectory(dir);
        var path = UniquePath(dir, Path.GetFileName(name));
        await using var source = await openRead();
        await using var target = File.Create(path);
        await source.CopyToAsync(target);
        return path;
    }

    /// <summary>
    /// Cómo enseñar dónde está algo: en móvil la ruta real no le dice nada a nadie, así que se
    /// nombra la carpeta tal como la ve el usuario (Archivos en iOS, almacenamiento interno en Android).
    /// </summary>
    public static string DisplayPath(string path)
    {
        if (!AppPlatform.IsMobile) return path;
        var visible = AppPaths.VisibleRoot
                      ?? (AppPaths.FixedLibraryRoot is { } documents ? (documents, "settings.library_ios_location") : null);
        if (visible is not { } shown) return path;
        var (root, nameKey) = shown;
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".") return L.Get(nameKey);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return path;
        return string.Join(" → ", [L.Get(nameKey), .. relative.Split(Path.DirectorySeparatorChar)]);
    }

    /// <summary>dir/name, o "name (2).ext", "name (3).ext"… si ya existe.</summary>
    internal static string UniquePath(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext  = Path.GetExtension(name);
        for (var n = 2; File.Exists(path) || Directory.Exists(path); n++)
            path = Path.Combine(dir, $"{stem} ({n}){ext}");
        return path;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        // iOS: la biblioteca es siempre la carpeta de Hakufu en Archivos.
        if (AppPaths.FixedLibraryRoot is not null || Storage is not { } storage) return null;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title         = title,
            AllowMultiple = false
        });
        return folders.FirstOrDefault() is { } folder ? folder.TryGetLocalPath() ?? ToLocalFolder(folder.Path) : null;
    }

    /// <summary>Android: la URI del selector, como ruta; si no es del dispositivo, FolderNotOnDeviceException.</summary>
    internal static string ToLocalFolder(Uri uri)
        => AndroidStorage.TreeUriToPath(uri) ?? throw new FolderNotOnDeviceException();

    public async Task<string?> SaveFileAsync(string title, string suggestedName, FileFilter filter)
    {
        // Móvil: sin selector, a una carpeta que se ve desde fuera (iOS: la de Hakufu en
        // Archivos; Android: Descargas). El escáner solo mira .cbz/.cbr/.pdf.
        if (AppPlatform.IsMobile && AppPaths.SaveDir is { } saveDir)
        {
            Directory.CreateDirectory(saveDir);
            return UniquePath(saveDir, suggestedName);
        }
        if (Storage is not { } storage) return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title             = title,
            SuggestedFileName = suggestedName,
            DefaultExtension  = filter.Extensions.FirstOrDefault(),
            FileTypeChoices   = [ToFileType(filter)],
            ShowOverwritePrompt = true
        });
        return file?.TryGetLocalPath();
    }

    public void OpenFolder(string path)
    {
        // No se crea nada: puede ser la carpeta del usuario (o un disco sin conectar).
        if (!Directory.Exists(path)) return;
        if (AppPlatform.OpenFolder is { } open) { open(path); return; } // iOS: la app Archivos
        var opener = OperatingSystem.IsWindows() ? "explorer.exe"
                   : OperatingSystem.IsMacOS()   ? "open"
                   : "xdg-open";
        try
        {
            var psi = new ProcessStartInfo(opener) { UseShellExecute = false };
            // En macOS "open" ejecuta un paquete .app aunque sea una carpeta: una
            // colección llamada "algo.app" (p. ej. de una copia de seguridad ajena)
            // se enseña en el Finder (-R) en vez de abrirse.
            if (OperatingSystem.IsMacOS() && !string.IsNullOrEmpty(Path.GetExtension(path.TrimEnd('/'))))
                psi.ArgumentList.Add("-R");
            psi.ArgumentList.Add(path);
            Process.Start(psi);
        }
        catch { /* sin explorador de ficheros disponible: se ignora */ }
    }
}
