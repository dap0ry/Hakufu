using System.IO;
using System.Net.Http;
using System.Text.Json;
using Hakufu.MVVM.Model;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Hakufu.Services;

public class StoreService : IStoreService
{
    private const string CatalogUrl = "https://raw.githubusercontent.com/dap0ry/Hakufu/main/catalog/catalog.json";

    private static readonly string DescargasDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Hakufu", "descargas");

    private static readonly HttpClient _http = new();

    static StoreService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("HakufuApp");
    }

    public async Task<MangaCatalog> FetchCatalogAsync(CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync(CatalogUrl, ct);
        return JsonSerializer.Deserialize<MangaCatalog>(json) ?? new MangaCatalog();
    }

    public async Task<string> DownloadMangaAsync(
        string url,
        string title,
        IProgress<(double pct, string status)> progress,
        CancellationToken ct = default)
    {
        var destDir = Path.Combine(DescargasDir, SanitizeFolderName(title));

        // Conservar la extensión original para que SharpCompress detecte el formato
        var urlPath = new Uri(url).AbsolutePath;
        var ext     = Path.GetExtension(urlPath);                        // .rar, .zip, …
        if (string.IsNullOrEmpty(ext)) ext = ".bin";
        var tempFile = Path.Combine(Path.GetTempPath(), $"hakufu_dl_{Guid.NewGuid():N}{ext}");

        Directory.CreateDirectory(destDir);

        // ── Descarga ────────────────────────────────────────────────────────
        progress.Report((0, "Conectando..."));

        using var response = await _http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total  = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long read  = 0;

        await using var src  = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(tempFile);

        int bytesRead;
        while ((bytesRead = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            read += bytesRead;
            if (total > 0)
                progress.Report(((double)read / total * 88, $"Descargando {(double)read / total * 100:0}%..."));
        }
        dest.Close();

        // ── Extracción (ZIP, RAR, 7z, tar… — SharpCompress lo detecta solo) ─
        progress.Report((90, "Extrayendo archivos..."));
        await Task.Run(() =>
        {
            using var stream = File.OpenRead(tempFile);
            using var reader = ReaderFactory.OpenReader(stream);
            reader.WriteAllToDirectory(destDir, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite       = true
            });
        }, ct);

        // ── Limpieza ────────────────────────────────────────────────────────
        try { File.Delete(tempFile); } catch { }

        progress.Report((100, "¡Listo!"));
        return destDir;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean   = new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "descarga" : clean;
    }
}
