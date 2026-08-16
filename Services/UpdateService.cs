using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Hakufu.MVVM.Model;
using Velopack;
using Velopack.Sources;

namespace Hakufu.Services;

public class UpdateService : IUpdateService
{
    private const string ApiUrl  = "https://api.github.com/repos/dap0ry/Hakufu/releases/latest";
    private const string RepoUrl = "https://github.com/dap0ry/Hakufu";
    private const string UserAgent = "HakufuApp";

    private static readonly HttpClient _http = new();

    private readonly UpdateManager _mgr = new(new GithubSource(RepoUrl, null, false));
    private UpdateInfo? _pendingUpdate;

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

    public bool IsUpdateReadyToApply => _pendingUpdate is not null;

    public async Task CheckForUpdatesInBackgroundAsync()
    {
        // Fuera de una instalación gestionada por Velopack (p. ej. `dotnet run`
        // en desarrollo) no hay nada que comprobar.
        if (!_mgr.IsInstalled)
            return;

        try
        {
            var updateInfo = await _mgr.CheckForUpdatesAsync();
            if (updateInfo is null)
                return;

            await _mgr.DownloadUpdatesAsync(updateInfo);
            _pendingUpdate = updateInfo;
        }
        catch
        {
            // Silencioso a propósito: una comprobación fallida en segundo
            // plano nunca debe interrumpir ni bloquear la app. El
            // changelog manual (FetchLatestReleaseAsync) sigue disponible
            // como vía alternativa para que el usuario vea si hay algo nuevo.
        }
    }

    public void ApplyUpdateAndRestart()
    {
        if (_pendingUpdate is null)
            return;

        _mgr.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
    }
}
