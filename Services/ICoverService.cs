using Avalonia.Media.Imaging;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public interface ICoverService
{
    /// <summary>Portada del manga (primera página / primera imagen), o null si no se puede leer.</summary>
    Task<Bitmap?> GetCoverAsync(Manga manga);

    /// <summary>Extracts cover to disk cache and returns the cache path.</summary>
    Task<string> ExtractAndCacheCoverAsync(string filePath, Guid mangaId);
}
