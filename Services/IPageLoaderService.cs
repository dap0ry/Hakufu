using System.Windows.Media.Imaging;

namespace Hakufu.Services;

public interface IPageLoaderService : IDisposable
{
    int TotalPages { get; }
    Task<BitmapSource?> LoadPageAsync(int pageIndex);
    void Preload(int currentPage);
}
