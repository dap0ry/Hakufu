using Hakufu.Data;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

/// <summary>Una colección en "Leyendo ahora": su último tomo leído y cuánto va leído en total.</summary>
public sealed record RecentCollection(Collection Collection, Manga LastVolume, int PagesRead, int TotalPages, DateTime LastRead)
{
    public double ProgressPct => TotalPages > 0 ? Math.Min(100, (double)PagesRead / TotalPages * 100) : 0;
}

public class ProfileService
{
    private readonly IDataRepository _repo;

    public ProfileService(IDataRepository repo) => _repo = repo;

    /// <summary>
    /// Páginas leídas: las pasadas en el lector (registro diario) o, si salen
    /// más, la suma de por dónde se va en cada tomo (lo leído antes de que
    /// existiera el registro).
    /// </summary>
    public int GetTotalPagesRead()
        => Math.Max(_repo.Current.ReadingLog.Sum(d => d.Pages),
                    _repo.Current.Progress.Sum(p => p.CurrentPage));

    // ── Datos del perfil (nombre, foto, favorito) ──────────────────────────

    public UserProfile GetProfile()
    {
        var p = _repo.Current.Profile;
        // "Desde": la primera vez que se mira el perfil, la fecha del manga más
        // antiguo (lo más parecido a cuándo se empezó a usar Hakufu).
        if (p.MemberSince is null)
            p.MemberSince = _repo.Current.Mangas.Count > 0
                ? _repo.Current.Mangas.Min(m => m.DateAdded)
                : DateTime.Now;
        return p;
    }

    /// <summary>Guarda nombre, favorito y, si se eligió una foto nueva, la copia a la carpeta de datos.</summary>
    public async Task UpdateProfileAsync(string name, Guid? favoriteMangaId, string? newAvatarSource, bool removeAvatar)
    {
        var p = GetProfile();
        p.Name = name.Trim();
        p.FavoriteMangaId = favoriteMangaId;

        if (removeAvatar || !string.IsNullOrEmpty(newAvatarSource))
        {
            var old = p.AvatarPath;
            p.AvatarPath = string.Empty;
            if (!string.IsNullOrEmpty(newAvatarSource))
            {
                Directory.CreateDirectory(AppPaths.ProfileDir);
                var dest = Path.Combine(AppPaths.ProfileDir,
                    $"avatar-{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(newAvatarSource).ToLowerInvariant()}");
                await Task.Run(() => File.Copy(newAvatarSource, dest, overwrite: true));
                p.AvatarPath = dest;
            }
            // Solo se borra la copia que hizo Hakufu: AvatarPath puede venir de
            // una copia de seguridad ajena y apuntar a cualquier archivo.
            if (IsInsideProfileDir(old) && old != p.AvatarPath)
                try { File.Delete(old); } catch { /* en uso o ya no está */ }
        }
        await _repo.SaveAsync();
    }

    private static bool IsInsideProfileDir(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var dir  = Path.GetFullPath(AppPaths.ProfileDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path);
        return full.StartsWith(dir, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public Manga? GetFavoriteManga()
    {
        var id = _repo.Current.Profile.FavoriteMangaId;
        return (id is null ? null : _repo.Current.Mangas.FirstOrDefault(m => m.Id == id))
               ?? _repo.Current.Mangas.Where(m => m.IsFavorite)
                                      .OrderByDescending(m => m.FavoritedAt ?? DateTime.MinValue)
                                      .FirstOrDefault();
    }

    /// <summary>
    /// Las últimas colecciones leídas (por el tomo leído más recientemente),
    /// con ese tomo (su portada) y las páginas leídas / totales de la colección.
    /// </summary>
    public IReadOnlyList<RecentCollection> GetRecentCollections(int count)
    {
        var store    = _repo.Current;
        var mangas   = store.Mangas.ToDictionary(m => m.Id);
        var progress = store.Progress.GroupBy(p => p.MangaId)
                                     .ToDictionary(g => g.Key, g => g.MaxBy(p => p.LastRead)!);
        var result = new List<RecentCollection>();
        foreach (var col in store.Collections)
        {
            var volumes = col.MangaIds.Select(id => mangas.GetValueOrDefault(id)).OfType<Manga>().ToList();
            var last = volumes.Where(m => progress.ContainsKey(m.Id))
                              .MaxBy(m => progress[m.Id].LastRead);
            if (last is null) continue;
            // CurrentPage empieza en 0: estar en la página n es haber leído n + 1.
            var read = volumes.Sum(m => progress.TryGetValue(m.Id, out var p)
                ? Math.Min(p.CurrentPage + 1, Math.Max(m.TotalPages, 0)) : 0);
            result.Add(new RecentCollection(col, last, read, volumes.Sum(m => Math.Max(m.TotalPages, 0)),
                                            progress[last.Id].LastRead));
        }
        return result.OrderByDescending(r => r.LastRead).Take(count).ToList();
    }

    public int GetFinishedCount() => _repo.Current.History.Select(h => h.MangaId).Distinct().Count();

    // ── Registro de lectura (gráficas de Perfil) ────────────────────────────

    /// <summary>Suma tiempo y páginas al día de hoy. No guarda: lo hace quien llama.</summary>
    public void LogReading(TimeSpan time, int pages)
    {
        if (time <= TimeSpan.Zero && pages <= 0) return;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var day = _repo.Current.ReadingLog.FirstOrDefault(d => d.Date == today);
        if (day is null)
        {
            day = new ReadingDay { Date = today };
            _repo.Current.ReadingLog.Add(day);
        }
        day.Seconds += Math.Max(0, time.TotalSeconds);
        day.Pages   += Math.Max(0, pages);
    }

    public Task SaveAsync() => _repo.SaveAsync();

    public IReadOnlyList<ReadingDay> GetReadingLog() => _repo.Current.ReadingLog;

    public TimeSpan GetTotalReadingTime()
        => TimeSpan.FromSeconds(_repo.Current.ReadingLog.Sum(d => d.Seconds));

    /// <summary>Horas leídas por semana (lunes a domingo), de la más antigua a la actual.</summary>
    public IReadOnlyList<(DateOnly WeekStart, double Hours, int Pages)> GetWeeklyReading(int weeks, DateOnly? today = null)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Now);
        var thisMonday = now.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        var result = new List<(DateOnly, double, int)>();
        for (var i = weeks - 1; i >= 0; i--)
        {
            var start = thisMonday.AddDays(-7 * i);
            var end   = start.AddDays(7);
            var days  = _repo.Current.ReadingLog.Where(d => d.Date >= start && d.Date < end).ToList();
            result.Add((start, days.Sum(d => d.Seconds) / 3600.0, days.Sum(d => d.Pages)));
        }
        return result;
    }

    /// <summary>Días seguidos leyendo hasta hoy (o hasta ayer, si hoy aún no se ha leído).</summary>
    public int GetStreakDays(DateOnly? today = null)
    {
        var now  = today ?? DateOnly.FromDateTime(DateTime.Now);
        var read = _repo.Current.ReadingLog.Where(d => d.Seconds >= 60 || d.Pages > 0)
                                           .Select(d => d.Date).ToHashSet();
        var day = read.Contains(now) ? now : now.AddDays(-1);
        var streak = 0;
        while (read.Contains(day)) { streak++; day = day.AddDays(-1); }
        return streak;
    }

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
