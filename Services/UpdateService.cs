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
        var appDir     = AppContext.BaseDirectory;
        // Descargamos a %TEMP% para no tocar la carpeta de la app en ejecución
        var tempDir    = Path.Combine(Path.GetTempPath(), "HakufuUpdate");
        var zipPath    = Path.Combine(tempDir, "update.zip");
        var updaterSrc = Path.Combine(appDir, "updater.exe");
        var updaterDst = Path.Combine(tempDir, "updater.exe");

        Directory.CreateDirectory(tempDir);

        // Descarga
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

        // Copia updater.exe a temp para que no lo sobreescriba la extracción
        File.Copy(updaterSrc, updaterDst, overwrite: true);

        // Lanza el updater desde temp con los argumentos necesarios.
        // Usamos ArgumentList para evitar problemas de quoting con rutas
        // que terminan en backslash (AppContext.BaseDirectory incluye /).
        var appExe = Path.Combine(appDir, "Hakufu.exe");
        var psi    = new ProcessStartInfo(updaterDst)
        {
            UseShellExecute = false   // evita SmartScreen y quoting del shell
        };
        psi.ArgumentList.Add(zipPath);
        psi.ArgumentList.Add(appDir.TrimEnd('\\', '/'));
        psi.ArgumentList.Add(appExe);
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(psi);

        Application.Current.Shutdown();
    }
}
