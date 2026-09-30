using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Hakufu.Services;

public class FilePickerService : IFilePickerService
{
    private static IStorageProvider? Storage =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.StorageProvider;

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
        return files.Select(f => f.TryGetLocalPath())
                    .OfType<string>()
                    .ToArray();
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedName, FileFilter filter)
    {
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
        Directory.CreateDirectory(path);
        var opener = OperatingSystem.IsWindows() ? "explorer.exe"
                   : OperatingSystem.IsMacOS()   ? "open"
                   : "xdg-open";
        try
        {
            var psi = new ProcessStartInfo(opener) { UseShellExecute = false };
            psi.ArgumentList.Add(path);
            Process.Start(psi);
        }
        catch { /* sin explorador de ficheros disponible: se ignora */ }
    }
}
