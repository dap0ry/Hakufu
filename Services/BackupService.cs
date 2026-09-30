using System.IO.Compression;
using System.Text.Json;
using Hakufu.Data;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public class BackupService(IDataRepository repo) : IBackupService
{
    private const string DataEntry     = "data.json";
    private const string ManifestEntry = "hakufu-backup.json";

    // Carpetas de AppPaths.DataDir que viajan en la copia (nombre en el zip = nombre en disco).
    private static readonly string[] MediaFolders = ["covers", "customization"];
    private const string LibraryFolder = "biblioteca";

    private sealed record Manifest(int Version, string DataDir, DateTime CreatedAt);

    public async Task ExportAsync(string zipPath, bool includeLibraryFiles, IProgress<double>? progress = null)
    {
        await repo.SaveAsync();

        var dataDir = AppPaths.DataDir;
        var folders = includeLibraryFiles ? [.. MediaFolders, LibraryFolder] : MediaFolders;
        var files = folders
            .Select(f => Path.Combine(dataDir, f))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            .ToList();

        await Task.Run(() =>
        {
            // Se escribe a un temporal y se mueve al final: si falla a medias no
            // queda un zip roto con el nombre que eligió el usuario.
            var tmp = zipPath + ".tmp";
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                WriteJson(zip, ManifestEntry, new Manifest(1, dataDir, DateTime.Now));
                WriteJson(zip, DataEntry, repo.Current);

                for (var i = 0; i < files.Count; i++)
                {
                    var rel = Path.GetRelativePath(dataDir, files[i]).Replace('\\', '/');
                    // Los mangas ya van comprimidos: no gastar CPU en recomprimirlos.
                    var level = rel.StartsWith(LibraryFolder + "/") ? CompressionLevel.NoCompression
                                                                    : CompressionLevel.Optimal;
                    zip.CreateEntryFromFile(files[i], rel, level);
                    progress?.Report((i + 1) / (double)Math.Max(1, files.Count));
                }
            }
            File.Move(tmp, zipPath, overwrite: true);
        });
        progress?.Report(1);
    }

    public async Task<bool> ImportAsync(string zipPath, IProgress<double>? progress = null)
    {
        if (!File.Exists(zipPath)) return false;

        var ok = await Task.Run(() =>
        {
            ZipArchive zip;
            try { zip = ZipFile.OpenRead(zipPath); }
            catch { return false; }

            using (zip)
            {
                var dataEntry = zip.GetEntry(DataEntry);
                if (dataEntry is null) return false;

                AppDataStore? store;
                try
                {
                    using var s = dataEntry.Open();
                    store = JsonSerializer.Deserialize<AppDataStore>(s, JsonDataRepository.JsonOptions);
                }
                catch { return false; }
                if (store is null) return false;

                var oldDataDir = ReadManifest(zip)?.DataDir;
                var dataDir    = AppPaths.DataDir;

                var media = zip.Entries
                    .Where(e => !string.IsNullOrEmpty(e.Name) && IsMediaEntry(e.FullName))
                    .ToList();
                for (var i = 0; i < media.Count; i++)
                {
                    var dest = Path.GetFullPath(Path.Combine(dataDir, media[i].FullName));
                    // Protección "zip slip": nada puede escribirse fuera de la carpeta de datos.
                    if (!dest.StartsWith(Path.GetFullPath(dataDir) + Path.DirectorySeparatorChar)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    media[i].ExtractToFile(dest, overwrite: true);
                    progress?.Report((i + 1) / (double)Math.Max(1, media.Count));
                }

                if (oldDataDir is not null)
                    RebasePaths(store, oldDataDir, dataDir);

                repo.Replace(store);
                return true;
            }
        });

        if (ok) await repo.SaveAsync();
        progress?.Report(1);
        return ok;
    }

    private static bool IsMediaEntry(string fullName) =>
        MediaFolders.Append(LibraryFolder).Any(f => fullName.StartsWith(f + "/"));

    private static void WriteJson<T>(ZipArchive zip, string name, T value)
    {
        using var s = zip.CreateEntry(name).Open();
        JsonSerializer.Serialize(s, value, JsonDataRepository.JsonOptions);
    }

    private static Manifest? ReadManifest(ZipArchive zip)
    {
        try
        {
            using var s = zip.GetEntry(ManifestEntry)?.Open();
            return s is null ? null : JsonSerializer.Deserialize<Manifest>(s);
        }
        catch { return null; }
    }

    /// <summary>
    /// Cambia el prefijo de la carpeta de datos del equipo de origen por la de
    /// este (p. ej. C:\Users\x\AppData\Roaming\Hakufu → /home/x/.config/Hakufu).
    /// Las rutas fuera de esa carpeta (mangas en otra ubicación) se dejan igual.
    /// </summary>
    internal static void RebasePaths(AppDataStore store, string oldDataDir, string newDataDir)
    {
        string Fix(string? path)
        {
            if (string.IsNullOrEmpty(path)) return path ?? "";
            var norm   = path.Replace('\\', '/');
            var oldDir = oldDataDir.Replace('\\', '/').TrimEnd('/');
            if (!norm.StartsWith(oldDir + "/", StringComparison.OrdinalIgnoreCase)) return path;
            var rel = norm[(oldDir.Length + 1)..];
            return Path.Combine([newDataDir, .. rel.Split('/')]);
        }

        foreach (var m in store.Mangas)
        {
            m.FilePath       = Fix(m.FilePath);
            m.CoverCachePath = Fix(m.CoverCachePath);
        }

        var c = store.Customization;
        foreach (var img in new[] { c.LeftPanelBackground, c.GeneralWallpaper }
                     .Concat(c.NavIcons.Values).Concat(c.NavBackgrounds.Values))
            if (img is not null) img.Path = Fix(img.Path);
    }
}
