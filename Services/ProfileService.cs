using Hakufu.Data;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public class ProfileService
{
    private readonly IDataRepository _repo;

    public ProfileService(IDataRepository repo) => _repo = repo;

    public FavoriteSlot[] GetFavorites()
    {
        var slots = _repo.Current.Favorites
            .OrderBy(f => f.SlotIndex)
            .Take(3)
            .ToArray();

        // Pad to 3 if needed
        if (slots.Length < 3)
        {
            var list = slots.ToList();
            while (list.Count < 3)
                list.Add(new FavoriteSlot { SlotIndex = list.Count });
            slots = [.. list];
        }
        return slots;
    }

    public async Task PinFavoriteAsync(int slot, Guid? mangaId)
    {
        var existing = _repo.Current.Favorites.FirstOrDefault(f => f.SlotIndex == slot);
        if (existing is null)
        {
            existing = new FavoriteSlot { SlotIndex = slot };
            _repo.Current.Favorites.Add(existing);
        }
        existing.MangaId = mangaId;
        await _repo.SaveAsync();
    }

    public IReadOnlyList<ReadingHistoryEntry> GetHistory()
        => _repo.Current.History.OrderByDescending(h => h.CompletedAt).ToList();

    public int GetTotalPagesRead()
        => _repo.Current.Progress.Sum(p => p.CurrentPage);

    public string GetFavoriteCollectionName()
    {
        var best = _repo.Current.Collections
            .Select(c => new {
                c.Name,
                Pages = _repo.Current.Progress
                    .Where(p => c.MangaIds.Contains(p.MangaId))
                    .Sum(p => p.CurrentPage)
            })
            .Where(x => x.Pages > 0)
            .OrderByDescending(x => x.Pages)
            .FirstOrDefault();
        return best?.Name ?? "—";
    }

    public IReadOnlyList<(string Name, int Pages)> GetCollectionStats()
        => _repo.Current.Collections
            .Select(c => (c.Name, Pages: _repo.Current.Progress
                .Where(p => c.MangaIds.Contains(p.MangaId))
                .Sum(p => p.CurrentPage)))
            .Where(x => x.Pages > 0)
            .OrderByDescending(x => x.Pages)
            .ToList();

    public TimeSpan GetTotalUsageTime()
        => TimeSpan.FromSeconds(_repo.Current.TotalUsageSeconds);

    public async Task AddHistoryEntryAsync(Guid mangaId)
    {
        // Only add if not already completed today
        var today = DateTime.Today;
        bool alreadyToday = _repo.Current.History
            .Any(h => h.MangaId == mangaId && h.CompletedAt.Date == today);
        if (!alreadyToday)
        {
            _repo.Current.History.Add(new ReadingHistoryEntry { MangaId = mangaId });
            await _repo.SaveAsync();
        }
    }
}
