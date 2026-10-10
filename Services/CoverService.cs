using Avalonia.Media.Imaging;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Hakufu.Services;

public class CoverService : ICoverService
{
    private static string CoverDir => AppPaths.CoversDir;

    // pdfium no es seguro entre hilos: un solo PDF a la vez (también al contar
    // páginas en LibraryScanner).
    internal static readonly SemaphoreSlim PdfLock = new(1, 1);

    internal static readonly HashSet<string> ImageExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];

    public async Task<Bitmap?> GetCoverAsync(Manga manga)
    {
        if (!string.IsNullOrEmpty(manga.CoverCachePath) && File.Exists(manga.CoverCachePath))
            return BitmapHelper.TryLoad(manga.CoverCachePath);

        var cachePath = await ExtractAndCacheCoverAsync(manga.FilePath, manga.Id);
        return string.IsNullOrEmpty(cachePath) ? null : BitmapHelper.TryLoad(cachePath);
    }

    public async Task<string> ExtractAndCacheCoverAsync(string filePath, Guid mangaId)
    {
        Directory.CreateDirectory(CoverDir);
        var cachePath = Path.Combine(CoverDir, $"{mangaId}.png");

        if (File.Exists(cachePath)) return cachePath;
        if (!File.Exists(filePath)) return string.Empty;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            if (ext == ".pdf")
                await ExtractPdfCoverAsync(filePath, cachePath);
            else if (ext is ".cbr" or ".cbz" or ".zip")
                await Task.Run(() => ExtractArchiveCover(filePath, cachePath));
            else
                return string.Empty;
        }
        catch
        {
            return string.Empty;
        }

        return File.Exists(cachePath) ? cachePath : string.Empty;
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    private static async Task ExtractPdfCoverAsync(string pdfPath, string cachePath)
    {
        await PdfLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var doc = PdfDocument.Open(pdfPath, 300, 450);
                using var bmp = doc.RenderPage(0);
                bmp.Save(cachePath);
            });
        }
        finally
        {
            PdfLock.Release();
        }
    }

    // ── CBR (RAR) / CBZ (ZIP) via SharpCompress ──────────────────────────────

    private static void ExtractArchiveCover(string archivePath, string cachePath)
    {
        // ArchiveFactory.OpenArchive handles both RAR and ZIP automatically
        using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());

        var entry = archive.Entries
            .Where(e => !e.IsDirectory &&
                        ImageExtensions.Contains(
                            Path.GetExtension(e.Key ?? "").ToLowerInvariant()))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (entry is null) return;

        using var ms = new MemoryStream();
        entry.WriteTo(ms);
        ms.Position = 0;

        using var bmp = BitmapHelper.FromStream(ms, decodeWidth: 300);
        bmp.Save(cachePath);
    }
}
