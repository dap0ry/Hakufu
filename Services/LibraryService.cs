using System.IO;
using Hakufu.Data;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public class LibraryService
{
    private readonly IDataRepository _repo;

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

    public async Task<Manga> AddMangaToCollectionAsync(
        Guid collectionId, string filePath, int totalPages, string coverCachePath,
        Guid? presetId = null)
    {
        // Reuse existing manga record if same file path already added
        var existing = _repo.Current.Mangas.FirstOrDefault(m => m.FilePath == filePath);
        Manga manga;
        if (existing is not null)
        {
            manga = existing;
        }
        else
        {
            var title = Path.GetFileNameWithoutExtension(filePath);
            manga = new Manga
            {
                Id             = presetId ?? Guid.NewGuid(),
                Title          = title,
                FilePath       = filePath,
                TotalPages     = totalPages,
                CoverCachePath = coverCachePath
            };
            _repo.Current.Mangas.Add(manga);
        }

        var col = GetCollection(collectionId);
        if (col is not null && !col.MangaIds.Contains(manga.Id))
        {
            col.MangaIds.Add(manga.Id);
            await _repo.SaveAsync();
        }
        return manga;
    }

    public async Task RemoveMangaFromCollectionAsync(Guid collectionId, Guid mangaId)
    {
        var col = GetCollection(collectionId);
        if (col is null) return;
        col.MangaIds.Remove(mangaId);
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
}
