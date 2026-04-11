using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public interface IStoreService
{
    MangaCatalog LoadCatalog();
    void OpenDownloadLink(string url);
}
