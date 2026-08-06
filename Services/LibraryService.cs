using System.IO;
using Hakufu.Data;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public class LibraryService
{
    private readonly IDataRepository _repo;

    private static readonly string BibliotecaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Hakufu", "biblioteca");

    public LibraryService(IDataRepository repo) => _repo = repo;

    public IReadOnlyList<Collection> GetCollections() => _repo.Current.Collections;

    public async Task<Collection> CreateCollectionAsync(string name, string description)
    {
        var col = new Collection { Name = name, Description = description };
        _repo.Current.Collections.Add(col);
        await _repo.SaveAsync();
        return col;
    }

    public async Task DeleteCollectionAsync(Guid collectionId)
    {
        _repo.Current.Collections.RemoveAll(c => c.Id == collectionId);
        await _repo.SaveAsync();
    }

    public Collection? GetCollection(Guid id)
        => _repo.Current.Collections.FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<Manga> GetMangasInCollection(Guid collectionId)
    {
        var col = GetCollection(collectionId);
        if (col is null) return [];
        return _repo.Current.Mangas
            .Where(m => col.MangaIds.Contains(m.Id))
            .ToList();
    }

    /// <summary>
    /// Mangas de la colección en el mismo orden que se ven al abrirla
    /// (respeta SortMode: nombre / fecha / personalizado). Usado también
    /// para elegir qué portada mostrar primero en la tarjeta de la colección.
    /// </summary>
    public IReadOnlyList<Manga> GetMangasInCollectionSorted(Guid collectionId)
        => SortMangas(GetMangasInCollection(collectionId), SortMode).ToList();

    public static IEnumerable<Manga> SortMangas(IEnumerable<Manga> mangas, string sortMode) => sortMode switch
    {
        "name"   => mangas.OrderBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase),
        "custom" => mangas.OrderBy(m => m.CustomOrder),
        _        => mangas.OrderByDescending(m => m.DateAdded), // "date"
    };

    // ── Favoritos ────────────────────────────────────────────────────────────

    public IReadOnlyList<Collection> GetFavoriteCollections()
        => _repo.Current.Collections.Where(c => c.IsFavorite).ToList();

    public async Task ToggleCollectionFavoriteAsync(Guid collectionId)
    {
        var col = GetCollection(collectionId);
        if (col is null) return;
        col.IsFavorite = !col.IsFavorite;
        await _repo.SaveAsync();
    }

    public IReadOnlyList<Manga> GetFavoriteMangas()
        => _repo.Current.Mangas
            .Where(m => m.IsFavorite)
            .OrderByDescending(m => m.FavoritedAt ?? DateTime.MinValue)
            .ToList();

    public async Task ToggleMangaFavoriteAsync(Guid mangaId)
    {
        var manga = GetManga(mangaId);
        if (manga is null) return;
        manga.IsFavorite = !manga.IsFavorite;
        manga.FavoritedAt = manga.IsFavorite ? DateTime.Now : null;
        await _repo.SaveAsync();
    }

    public async Task<Manga> AddMangaToCollectionAsync(
        Guid collectionId, string filePath, int totalPages, string coverCachePath,
        Guid? presetId = null)
    {
        // Copy the file into biblioteca only if it is not already there
        var localPath = filePath;
        if (!filePath.StartsWith(BibliotecaDir, StringComparison.OrdinalIgnoreCase))
        {
            var col = GetCollection(collectionId);
            if (col is not null)
                localPath = await CopyToLibraryAsync(filePath, col.Name);
        }

        // Reuse existing manga record if same local path already added
        var existing = _repo.Current.Mangas.FirstOrDefault(m => m.FilePath == localPath);
        Manga manga;
        if (existing is not null)
        {
            manga = existing;
        }
        else
        {
            var title = Path.GetFileNameWithoutExtension(localPath);
            manga = new Manga
            {
                Id             = presetId ?? Guid.NewGuid(),
                Title          = title,
                FilePath       = localPath,
                TotalPages     = totalPages,
                CoverCachePath = coverCachePath
            };
            _repo.Current.Mangas.Add(manga);
        }

        var target = GetCollection(collectionId);
        if (target is not null && !target.MangaIds.Contains(manga.Id))
        {
            target.MangaIds.Add(manga.Id);
            await _repo.SaveAsync();
        }
        return manga;
    }

    /// <summary>
    /// Copies every manga whose FilePath is outside biblioteca to its collection folder
    /// inside biblioteca and updates FilePath. The original file is NOT deleted.
    /// Returns the number of files copied.
    /// </summary>
    public async Task<int> MigrateToLibraryAsync(IProgress<string>? progress = null)
    {
        var toMigrate = _repo.Current.Mangas
            .Where(m => !string.IsNullOrEmpty(m.FilePath) &&
                        !m.FilePath.StartsWith(BibliotecaDir, StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(m.FilePath))
            .ToList();

        int count = 0;
        foreach (var manga in toMigrate)
        {
            var collection = _repo.Current.Collections
                .FirstOrDefault(c => c.MangaIds.Contains(manga.Id));
            var folderName = collection?.Name ?? "sin_coleccion";

            progress?.Report(Path.GetFileName(manga.FilePath));

            var newPath = await CopyToLibraryAsync(manga.FilePath, folderName);
            manga.FilePath = newPath;

            count++;
        }

        if (count > 0)
            await _repo.SaveAsync();

        return count;
    }

    /// <summary>
    /// Returns how many manga files currently live outside biblioteca.
    /// </summary>
    public int CountExternalMangas() =>
        _repo.Current.Mangas.Count(m =>
            !string.IsNullOrEmpty(m.FilePath) &&
            !m.FilePath.StartsWith(BibliotecaDir, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(m.FilePath));

    private static async Task<string> CopyToLibraryAsync(string sourcePath, string collectionName)
    {
        var folderName = SanitizeFolderName(collectionName);
        var destDir    = Path.Combine(BibliotecaDir, folderName);
        Directory.CreateDirectory(destDir);

        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destDir, fileName);

        // Avoid overwriting a different file that happens to share the name
        if (File.Exists(destPath) &&
            !string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath),
                           StringComparison.OrdinalIgnoreCase))
        {
            var stem      = Path.GetFileNameWithoutExtension(fileName);
            var ext       = Path.GetExtension(fileName);
            var counter   = 1;
            do { destPath = Path.Combine(destDir, $"{stem} ({counter++}){ext}"); }
            while (File.Exists(destPath));
        }

        if (!File.Exists(destPath))
            await Task.Run(() => File.Copy(sourcePath, destPath));

        return destPath;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean   = new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "sin_nombre" : clean;
    }

    public async Task RemoveMangaFromCollectionAsync(Guid collectionId, Guid mangaId)
    {
        var col = GetCollection(collectionId);
        if (col is null) return;
        col.MangaIds.Remove(mangaId);
        await _repo.SaveAsync();
    }

    // ── Borrado real (libera espacio en disco) ─────────────────────────────────
    // Usado desde "Gestionar espacio": a diferencia de RemoveMangaFromCollectionAsync
    // (que solo quita la referencia), estos métodos también borran el archivo del
    // manga y su portada en caché — pero solo cuando el manga no queda referenciado
    // por ninguna otra colección, para no romper mangas compartidos entre varias.

    /// <summary>Borra la colección entera junto con los archivos de los mangas que no estén en otra colección.</summary>
    public async Task DeleteCollectionWithFilesAsync(Guid collectionId)
    {
        var col = GetCollection(collectionId);
        if (col is null) return;

        var mangaIds = col.MangaIds.ToList();
        _repo.Current.Collections.Remove(col);

        foreach (var mangaId in mangaIds)
            DeleteMangaIfOrphaned(mangaId);

        await _repo.SaveAsync();
    }

    /// <summary>Quita un tomo de una colección concreta y borra su archivo si no queda en ninguna otra.</summary>
    public async Task DeleteMangaFromCollectionWithFileAsync(Guid collectionId, Guid mangaId)
    {
        var col = GetCollection(collectionId);
        col?.MangaIds.Remove(mangaId);
        DeleteMangaIfOrphaned(mangaId);
        await _repo.SaveAsync();
    }

    private void DeleteMangaIfOrphaned(Guid mangaId)
    {
        bool stillReferenced = _repo.Current.Collections.Any(c => c.MangaIds.Contains(mangaId));
        if (stillReferenced) return;

        var manga = GetManga(mangaId);
        if (manga is null) return;

        try
        {
            if (!string.IsNullOrEmpty(manga.FilePath) && File.Exists(manga.FilePath))
                File.Delete(manga.FilePath);
        }
        catch { /* archivo bloqueado o ya borrado */ }

        try
        {
            if (!string.IsNullOrEmpty(manga.CoverCachePath) && File.Exists(manga.CoverCachePath))
                File.Delete(manga.CoverCachePath);
        }
        catch { /* ídem */ }

        _repo.Current.Mangas.Remove(manga);
        _repo.Current.Progress.RemoveAll(p => p.MangaId == mangaId);
    }

    public Manga? GetManga(Guid id)
        => _repo.Current.Mangas.FirstOrDefault(m => m.Id == id);

    public ReadingProgress? GetProgress(Guid mangaId)
        => _repo.Current.Progress.FirstOrDefault(p => p.MangaId == mangaId);

    public async Task SaveProgressAsync(Guid mangaId, int page)
    {
        var p = _repo.Current.Progress.FirstOrDefault(x => x.MangaId == mangaId);
        if (p is null)
        {
            p = new ReadingProgress { MangaId = mangaId };
            _repo.Current.Progress.Add(p);
        }
        p.CurrentPage = page;
        p.LastRead = DateTime.Now;
        await _repo.SaveAsync();
    }

    public Manga? GetLastReadManga()
    {
        var latest = _repo.Current.Progress
            .OrderByDescending(p => p.LastRead)
            .FirstOrDefault();
        if (latest is null) return null;
        return GetManga(latest.MangaId);
    }

    public string SortMode
    {
        get => _repo.Current.LibrarySortMode;
        set => _repo.Current.LibrarySortMode = value;
    }

    public Task SaveAsync() => _repo.SaveAsync();
}
