using System.Windows.Media.Imaging;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public interface ICoverService
{
    /// <summary>Returns a frozen BitmapSource for the manga cover (first page / first image).</summary>
    Task<BitmapSource?> GetCoverAsync(Manga manga);

    /// <summary>Extracts cover to disk cache and returns the cache path.</summary>
    Task<string> ExtractAndCacheCoverAsync(string filePath, Guid mangaId);
}
