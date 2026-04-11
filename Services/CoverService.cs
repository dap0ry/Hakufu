using System.IO;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using Hakufu.MVVM.Model;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Hakufu.Services;

public class CoverService : ICoverService
{
    private static readonly string CoverDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hakufu", "covers");

    private static readonly SemaphoreSlim PdfLock = new(1, 1);

    private static readonly HashSet<string> ImageExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];

    public async Task<BitmapSource?> GetCoverAsync(Manga manga)
    {
        if (!string.IsNullOrEmpty(manga.CoverCachePath) && File.Exists(manga.CoverCachePath))
            return LoadFromDisk(manga.CoverCachePath);

        var cachePath = await ExtractAndCacheCoverAsync(manga.FilePath, manga.Id);
        if (string.IsNullOrEmpty(cachePath)) return null;
        return LoadFromDisk(cachePath);
    }

    public async Task<string> ExtractAndCacheCoverAsync(string filePath, Guid mangaId)
    {
        Directory.CreateDirectory(CoverDir);
        var cachePath = Path.Combine(CoverDir, $"{mangaId}.png");

        if (File.Exists(cachePath)) return cachePath;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            if (ext == ".pdf")
                await ExtractPdfCoverAsync(filePath, cachePath);
            else if (ext is ".cbr" or ".cbz" or ".zip")
                await ExtractArchiveCoverAsync(filePath, cachePath);
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

    private async Task ExtractPdfCoverAsync(string pdfPath, string cachePath)
    {
        await PdfLock.WaitAsync();
        try
        {
            var bitmapSource = await Task.Run(() =>
            {
                using var docReader = DocLib.Instance.GetDocReader(
                    pdfPath, new PageDimensions(300, 450));
                using var pageReader = docReader.GetPageReader(0);

                int w   = pageReader.GetPageWidth();
                int h   = pageReader.GetPageHeight();
                byte[] raw = pageReader.GetImage();   // BGRA

                var bmp = BitmapSource.Create(w, h, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null, raw, w * 4);
                bmp.Freeze();
                return bmp;
            });

            SaveBitmapToDisk(bitmapSource, cachePath);
        }
        finally
        {
            PdfLock.Release();
        }
    }

    // ── CBR (RAR) / CBZ (ZIP) via SharpCompress ──────────────────────────────

    private static async Task ExtractArchiveCoverAsync(string archivePath, string cachePath)
    {
        var bitmapSource = await Task.Run<BitmapSource?>(() =>
        {
            // ArchiveFactory.Open handles both RAR and ZIP automatically
            using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());

            var entry = archive.Entries
                .Where(e => !e.IsDirectory &&
                            ImageExtensions.Contains(
                                Path.GetExtension(e.Key ?? "").ToLowerInvariant()))
                .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (entry is null) return null;

            using var ms = new MemoryStream();
            entry.WriteTo(ms);
            ms.Position = 0;

            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption      = BitmapCacheOption.OnLoad;
            img.StreamSource     = ms;
            img.DecodePixelWidth = 300;
            img.EndInit();
            img.Freeze();
            return img;
        });

        if (bitmapSource is not null)
            SaveBitmapToDisk(bitmapSource, cachePath);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BitmapSource? LoadFromDisk(string path)
    {
        try
        {
            var img = new BitmapImage(new Uri(path, UriKind.Absolute));
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    private static void SaveBitmapToDisk(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
