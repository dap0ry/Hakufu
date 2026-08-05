using Hakufu.Data;

namespace Hakufu.Services;

// Construye el payload de /users/me/library a partir del estado local. Lo usan
// tanto la sincronización de cuenta (SyncViewModel) como la copia de seguridad en
// Drive (BackupViewModel) — un único sitio donde mapear Manga/Collection/etc. al
// formato que espera la API.
public static class SyncPayloadBuilder
{
    public static HakufuApiClient.LibrarySyncPayload Build(IDataRepository repo)
    {
        var mangas      = repo.Current.Mangas;
        var collections = repo.Current.Collections;

        var history = repo.Current.History.Select(h =>
        {
            var m = mangas.FirstOrDefault(x => x.Id == h.MangaId);
            return new HakufuApiClient.HistorySyncItem(
                h.MangaId.ToString(),
                m?.Title              ?? "",
                m?.CloudinaryCoverUrl ?? "",
                h.CompletedAt);
        }).ToList();

        return new HakufuApiClient.LibrarySyncPayload(
            Mangas: mangas.Select(m => new HakufuApiClient.MangaSyncItem(
                m.Id.ToString(), m.Title, m.TotalPages,
                m.CloudinaryCoverUrl, m.DateAdded, m.DriveFileId)).ToList(),
            Collections: collections.Select(c => new HakufuApiClient.CollectionSyncItem(
                c.Id.ToString(), c.Name, c.Description,
                c.MangaIds.Select(id => id.ToString()).ToList(), c.CreatedAt)).ToList(),
            ReadingProgress: repo.Current.Progress.Select(p => new HakufuApiClient.ProgressSyncItem(
                p.MangaId.ToString(), p.CurrentPage, p.LastRead)).ToList(),
            ReadingHistory: history,
            TotalUsageSeconds: repo.Current.TotalUsageSeconds);
    }
}
