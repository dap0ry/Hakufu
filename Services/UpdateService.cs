using Velopack;
using Velopack.Sources;

namespace Hakufu.Services;

public class UpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/dap0ry/Hakufu";

    private readonly UpdateManager _mgr = new(new GithubSource(RepoUrl, null, false));

    public bool IsUpdateReadyToApply
        => _mgr.IsInstalled && _mgr.UpdatePendingRestart is not null;

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
        }
        catch
        {
            // Silencioso a propósito: una comprobación fallida en segundo
            // plano nunca debe interrumpir ni bloquear la app.
        }
    }

    public void ApplyUpdateAndRestart()
    {
        var pending = _mgr.UpdatePendingRestart;
        if (pending is null)
            return;

        _mgr.ApplyUpdatesAndRestart(pending);
    }
}
