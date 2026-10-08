using System.Net.Http.Headers;
using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace Hakufu.Services;

/// <summary>
/// ÚNICA parte de Hakufu que usa la red: comprueba y descarga versiones nuevas de
/// las releases de GitHub. Instalada con Velopack se actualiza sola; si no (zip
/// portable o dotnet run), solo consulta la última release para avisar.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    public const string RepoUrl = "https://github.com/dap0ry/Hakufu";
    private const string LatestApi = "https://api.github.com/repos/dap0ry/Hakufu/releases/latest";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly UpdateManager? _velopack;
    private readonly HttpClient _http;
    private UpdateInfo? _pending;
    private VelopackAsset? _downloaded;

    /// <param name="http">Solo para tests: fuerza el camino de la API de GitHub con una red falsa.</param>
    /// <param name="localSource">Carpeta de releases en vez de GitHub (o la variable HAKUFU_UPDATE_SOURCE),
    /// para probar actualizaciones de verdad sin publicar nada.</param>
    public UpdateService(HttpMessageHandler? http = null, string? localSource = null)
    {
        _http = http is null ? new HttpClient() : new HttpClient(http);
        _http.Timeout = Timeout;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Hakufu", AppVersion.Current));

        if (http is not null) return;
        localSource ??= Environment.GetEnvironmentVariable("HAKUFU_UPDATE_SOURCE");
        IUpdateSource source = string.IsNullOrEmpty(localSource)
            ? new GithubSource(RepoUrl, null, false)
            : new SimpleFileSource(new DirectoryInfo(localSource));
        try
        {
            var mgr = new UpdateManager(source);
            _velopack = mgr.IsInstalled ? mgr : null;
        }
        catch
        {
            _velopack = null;
        }
    }

    public string CurrentVersion => _velopack?.CurrentVersion?.ToString() ?? AppVersion.Current;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            if (_velopack is not null)
            {
                // Ya descargada: no se cambia por otra que no se ha bajado.
                if (_downloaded is not null)
                    return new(UpdateCheckStatus.Available, _downloaded.Version.ToString(), CanSelfUpdate: true);
                _pending = await _velopack.CheckForUpdatesAsync().WaitAsync(Timeout, ct);
                return _pending is null
                    ? new(UpdateCheckStatus.UpToDate)
                    : new(UpdateCheckStatus.Available, _pending.TargetFullRelease.Version.ToString(), CanSelfUpdate: true);
            }

            using var res = await _http.GetAsync(LatestApi, ct);
            if (!res.IsSuccessStatusCode) return new(UpdateCheckStatus.Failed);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            return AppVersion.IsNewer(tag, AppVersion.Current)
                ? new(UpdateCheckStatus.Available, tag.TrimStart('v', 'V'))
                : new(UpdateCheckStatus.UpToDate);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new(UpdateCheckStatus.Failed);
        }
    }

    public async Task DownloadAsync(Action<int> progress, CancellationToken ct = default)
    {
        if (_velopack is null || _pending is null) throw new InvalidOperationException("Nada que descargar.");
        var target = _pending;
        await _velopack.DownloadUpdatesAsync(target, progress, ct);
        _downloaded = target.TargetFullRelease;
    }

    public void ApplyAndRestart()
    {
        if (_velopack is null || _downloaded is null) return;
        _velopack.ApplyUpdatesAndRestart(_downloaded);
    }
}
