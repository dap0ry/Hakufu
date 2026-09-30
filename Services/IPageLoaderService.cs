using Avalonia.Media.Imaging;

namespace Hakufu.Services;

public interface IPageLoaderService : IDisposable
{
    int TotalPages { get; }
    Task<Bitmap?> LoadPageAsync(int pageIndex);
    void Preload(int currentPage);
}
