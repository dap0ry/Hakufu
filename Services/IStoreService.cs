using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public interface IStoreService
{
    Task<MangaCatalog> FetchCatalogAsync(CancellationToken ct = default);

    /// <summary>
    /// Descarga el ZIP de <paramref name="url"/>, lo extrae en
    /// %APPDATA%\Hakufu\descargas\{title}\ y devuelve esa carpeta.
    /// </summary>
    Task<string> DownloadMangaAsync(
        string url,
        string title,
        IProgress<(double pct, string status)> progress,
        CancellationToken ct = default);
}
