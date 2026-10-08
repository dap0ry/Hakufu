using Hakufu.Data;
using Hakufu.I18n;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public class LibraryService
{
    private readonly IDataRepository _repo;

    public LibraryService(IDataRepository repo) => _repo = repo;

    public IReadOnlyList<Collection> GetCollections() => _repo.Current.Collections;

    /// <summary>
    /// Nombre de la colección para mostrar. La de tomos sueltos en la raíz se guarda como
    /// LibraryScanner.LooseCollectionName («Sin colección», dato usado al emparejar carpetas)
    /// y se traduce solo al pintarla; las demás muestran el nombre de su carpeta.
    /// </summary>
    public static string DisplayName(Collection collection)
        => string.IsNullOrEmpty(collection.RelativePath) && collection.Name == LibraryScanner.LooseCollectionName
            ? L.Get("library.loose_collection")
            : collection.Name;

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

    // Empates (p. ej. tomos encontrados en la misma lectura de la carpeta): por nombre.
    public static IEnumerable<Manga> SortMangas(IEnumerable<Manga> mangas, string sortMode) => (sortMode switch
    {
        "name"   => mangas.OrderBy(m => m.Title, NaturalComparer.Instance),
        "custom" => mangas.OrderBy(m => m.CustomOrder),
        _        => mangas.OrderByDescending(m => m.DateAdded), // "date"
    }).ThenBy(m => m.Title, NaturalComparer.Instance);

    /// <summary>Carpeta en disco de la colección (dentro de la biblioteca), o null si no se sabe.</summary>
    public string? GetCollectionFolder(Guid collectionId)
    {
        var root = _repo.Current.LibraryRoot;
        if (string.IsNullOrEmpty(root) || GetCollection(collectionId)?.RelativePath is not { } rel) return null;
        return rel.Length == 0 ? root : Path.Combine(root, rel);
    }

    /// <summary>Guarda las páginas de un tomo cuando se averiguan (al abrirlo en el lector).</summary>
    public async Task SetTotalPagesAsync(Guid mangaId, int totalPages)
    {
        if (totalPages <= 0 || GetManga(mangaId) is not { } manga || manga.TotalPages == totalPages) return;
        manga.TotalPages = totalPages;
        await _repo.SaveAsync();
    }

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

    public IReadOnlyList<Manga> GetAllMangas() => _repo.Current.Mangas.ToList();

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
