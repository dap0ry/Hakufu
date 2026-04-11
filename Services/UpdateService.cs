using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public class UpdateService : IUpdateService
{
    private const string ApiUrl    = "https://api.github.com/repos/dap0ry/Hakufu/releases/latest";
    private const string UserAgent = "HakufuApp";

    private static readonly HttpClient _http = new();

    static UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public Version GetCurrentVersion()
        => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);

    public async Task<GitHubRelease?> FetchLatestReleaseAsync()
    {
        var json = await _http.GetStringAsync(ApiUrl);
        return JsonSerializer.Deserialize<GitHubRelease>(json);
    }

    public async Task DownloadAndInstallAsync(
        string url,
        IProgress<double> progress,
        CancellationToken ct = default)
    {
        var appDir  = AppContext.BaseDirectory;
        var zipPath = Path.Combine(appDir, "update.zip");

        using var response = await _http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total  = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long read  = 0;

        await using var src  = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(zipPath);

        int bytesRead;
        while ((bytesRead = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            read += bytesRead;
            if (total > 0)
                progress.Report((double)read / total * 100);
        }

        dest.Close();

        ZipFile.ExtractToDirectory(zipPath, appDir, overwriteFiles: true);
        File.Delete(zipPath);

        var updaterPath = Path.Combine(appDir, "updater.exe");
        Process.Start(new ProcessStartInfo(updaterPath) { UseShellExecute = true });
        Application.Current.Shutdown();
    }
}
