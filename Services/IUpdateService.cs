using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public interface IUpdateService
{
    Version GetCurrentVersion();
    Task<GitHubRelease?> FetchLatestReleaseAsync();
    Task DownloadAndInstallAsync(string url, IProgress<double> progress, CancellationToken ct = default);
}
