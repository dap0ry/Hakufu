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
    // Solo la foto de perfil: los mangas son la carpeta del usuario y las
    // portadas se regeneran solas. Al restaurar se aceptan también las de
    // copias antiguas (covers/).
    private static readonly string[] ExportFolders = ["profile"];
    private static readonly string[] MediaFolders  = ["covers", "profile"];
    // Copias de versiones antiguas que sí traían los mangas copiados a Hakufu:
    // se siguen restaurando para no perderlos (quedan en AppPaths.LibraryDir).
    private const string LegacyLibraryFolder = "biblioteca";

    // Versión 1: copia completa que sustituye la biblioteca (hasta la 0.10.0).
    // Versión 2: perfil + colecciones elegidas, que se combina con lo que hay.
    private const int CurrentVersion = 2;
    private sealed record Manifest(int Version, string DataDir, DateTime CreatedAt);

    public async Task ExportAsync(string zipPath, BackupOptions options, IProgress<double>? progress = null)
    {
        await repo.SaveAsync();

        var dataDir = AppPaths.DataDir;
        var subset  = Subset(repo.Current, options);
        var files = ExportFolders
            .Select(f => Path.Combine(dataDir, f))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            // de la carpeta de perfil, solo la foto que se usa ahora
            .Where(f => string.Equals(Path.GetFullPath(f), FullOrEmpty(subset.Profile.AvatarPath)))
            .ToList();

        await Task.Run(() =>
        {
            // Se escribe a un temporal y se mueve al final: si falla a medias no
            // queda un zip roto con el nombre que eligió el usuario.
            var tmp = zipPath + ".tmp";
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                WriteJson(zip, ManifestEntry, new Manifest(CurrentVersion, dataDir, DateTime.Now));
                WriteJson(zip, DataEntry, subset);

                for (var i = 0; i < files.Count; i++)
                {
                    var rel = Path.GetRelativePath(dataDir, files[i]).Replace('\\', '/');
                    zip.CreateEntryFromFile(files[i], rel, CompressionLevel.Optimal);
                    progress?.Report((i + 1) / (double)Math.Max(1, files.Count));
                }
            }
            File.Move(tmp, zipPath, overwrite: true);
        });
        progress?.Report(1);
    }

    private static string FullOrEmpty(string? path) => string.IsNullOrEmpty(path) ? "" : Path.GetFullPath(path);

    /// <summary>Lo que va en la copia: perfil y ajustes, y las colecciones elegidas con lo suyo.</summary>
    internal static AppDataStore Subset(AppDataStore all, BackupOptions options)
    {
        var cols   = all.Collections.Where(c => options.CollectionIds.Contains(c.Id)).ToList();
        var ids    = cols.SelectMany(c => c.MangaIds).ToHashSet();
        return new AppDataStore
        {
            Profile           = all.Profile,
            ReadingLog        = all.ReadingLog,
            ActiveTheme       = all.ActiveTheme,
            Reader            = all.Reader,
            TotalUsageSeconds = all.TotalUsageSeconds,
            LibrarySortMode   = all.LibrarySortMode,
            Collections       = cols,
            Mangas            = all.Mangas.Where(m => ids.Contains(m.Id)).ToList(),
            Progress          = all.Progress.Where(p => ids.Contains(p.MangaId)).ToList(),
            History           = all.History.Where(h => ids.Contains(h.MangaId)).ToList(),
            // La carpeta es de cada equipo: no viaja.
            LibraryRoot       = "",
        };
    }

    public async Task<ImportResult> ImportAsync(string zipPath, IProgress<double>? progress = null)
    {
        if (!File.Exists(zipPath)) return ImportResult.Invalid;

        var result = await Task.Run(() =>
        {
            ZipArchive zip;
            try { zip = ZipFile.OpenRead(zipPath); }
            catch { return ImportResult.Invalid; }

            using (zip)
            {
                // Sin el manifiesto no es una copia de Hakufu: cualquier otro zip
                // con un "data.json" dentro vaciaría la biblioteca.
                if (ReadManifest(zip) is not { } manifest) return ImportResult.Invalid;

                var dataEntry = zip.GetEntry(DataEntry);
                if (dataEntry is null) return ImportResult.Invalid;

                AppDataStore? store;
                try
                {
                    using var s = dataEntry.Open();
                    store = JsonSerializer.Deserialize<AppDataStore>(s, JsonDataRepository.JsonOptions);
                }
                catch { return ImportResult.Invalid; }
                if (store is null) return ImportResult.Invalid;

                var dataDir = AppPaths.DataDir;

                // Primero todo a una carpeta temporal y después se mueve: si un
                // fichero falla (nombre no válido en este sistema, disco lleno…)
                // la biblioteca actual queda intacta.
                var staging = Path.Combine(dataDir, $".import-{Guid.NewGuid():N}");
                try
                {
                    var stagingFull = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                    var media = zip.Entries
                        .Where(e => !string.IsNullOrEmpty(e.Name) && IsMediaEntry(e.FullName))
                        .ToList();
                    var extracted = new List<string>();
                    for (var i = 0; i < media.Count; i++)
                    {
                        var dest = Path.GetFullPath(Path.Combine(staging, media[i].FullName));
                        // Protección "zip slip": nada puede escribirse fuera de la carpeta temporal.
                        if (!dest.StartsWith(stagingFull)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        media[i].ExtractToFile(dest, overwrite: true);
                        extracted.Add(dest);
                        progress?.Report((i + 1) / (double)Math.Max(1, media.Count) * 0.9);
                    }

                    foreach (var file in extracted)
                    {
                        var target = Path.Combine(dataDir, Path.GetRelativePath(staging, file));
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Move(file, target, overwrite: true);
                    }
                }
                catch
                {
                    return ImportResult.Invalid;
                }
                finally
                {
                    try { Directory.Delete(staging, recursive: true); } catch { }
                }

                RebasePaths(store, manifest.DataDir, dataDir);

                if (manifest.Version < 2)
                {
                    // Copia antigua (completa): sustituye la biblioteca. La carpeta de
                    // la biblioteca es la de este equipo; los tomos se vuelven a
                    // encontrar por su ruta relativa al leerla.
                    store.LibraryRoot = repo.Current.LibraryRoot;
                    repo.Replace(store);
                    return new ImportResult(true, store.Mangas.Count, 0, Legacy: true);
                }
                return Merge(repo.Current, store);
            }
        });

        if (result.Ok) await repo.SaveAsync();
        progress?.Report(1);
        return result;
    }

    /// <summary>
    /// Combina una copia (versión 2) con la biblioteca actual, que ya debe estar
    /// leída de la carpeta: perfil y ajustes de la copia; progreso, favoritos,
    /// orden e historial para los tomos que coincidan por ruta relativa.
    /// </summary>
    internal static ImportResult Merge(AppDataStore current, AppDataStore backup)
    {
        static bool Same(string? a, string? b) =>
            !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // Perfil y ajustes
        var myFavorite      = current.Profile.FavoriteMangaId;
        current.Profile     = backup.Profile;
        current.ActiveTheme = backup.ActiveTheme;
        current.Reader      = backup.Reader;
        current.TotalUsageSeconds = Math.Max(current.TotalUsageSeconds, backup.TotalUsageSeconds);
        foreach (var day in backup.ReadingLog)
        {
            var mine = current.ReadingLog.FirstOrDefault(d => d.Date == day.Date);
            if (mine is null) current.ReadingLog.Add(day);
            else if (day.Seconds > mine.Seconds) { mine.Seconds = day.Seconds; mine.Pages = Math.Max(mine.Pages, day.Pages); }
        }

        // Colecciones: favorita y descripción
        foreach (var bc in backup.Collections)
        {
            var cc = current.Collections.FirstOrDefault(c => Same(c.RelativePath, bc.RelativePath)) ??
                     current.Collections.FirstOrDefault(c => Same(c.Name, bc.Name));
            if (cc is null) continue;
            cc.IsFavorite = bc.IsFavorite;
            if (!string.IsNullOrWhiteSpace(bc.Description)) cc.Description = bc.Description;
        }

        // Tomos: por ruta relativa dentro de la carpeta de la biblioteca
        var applied = 0;
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var bm in backup.Mangas)
        {
            var cm = current.Mangas.FirstOrDefault(m => Same(m.RelativePath, bm.RelativePath));
            if (cm is null) continue;
            applied++;
            idMap[bm.Id] = cm.Id;
            cm.IsFavorite  = bm.IsFavorite;
            cm.FavoritedAt = bm.FavoritedAt;
            cm.CustomOrder = bm.CustomOrder;

            if (backup.Progress.FirstOrDefault(p => p.MangaId == bm.Id) is { } bp)
            {
                current.Progress.RemoveAll(p => p.MangaId == cm.Id);
                current.Progress.Add(new ReadingProgress { MangaId = cm.Id, CurrentPage = bp.CurrentPage, LastRead = bp.LastRead });
            }
            foreach (var h in backup.History.Where(h => h.MangaId == bm.Id))
                if (!current.History.Any(x => x.MangaId == cm.Id && x.CompletedAt == h.CompletedAt))
                    current.History.Add(new ReadingHistoryEntry { MangaId = cm.Id, CompletedAt = h.CompletedAt });
        }

        // El manga favorito del perfil apunta al tomo de este equipo; si no vino
        // en la copia, se queda el que ya había aquí.
        current.Profile.FavoriteMangaId = current.Profile.FavoriteMangaId is { } fav && idMap.TryGetValue(fav, out var here)
            ? here
            : myFavorite;

        return new ImportResult(true, applied, backup.Mangas.Count - applied);
    }

    private static bool IsMediaEntry(string fullName) =>
        MediaFolders.Append(LegacyLibraryFolder).Any(f => fullName.StartsWith(f + "/"));

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

        store.Profile.AvatarPath = Fix(store.Profile.AvatarPath);
    }
}
