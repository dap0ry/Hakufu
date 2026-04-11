using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using Hakufu.MVVM.Model;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Hakufu.Services;

public class PageLoaderService : IPageLoaderService
{
    private static readonly HashSet<string> ImageExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];

    private readonly string _ext;
    private readonly string _filePath;

    // PDF-specific
    private IDocReader? _docReader;

    // CBR/CBZ-specific — sorted list of (entryKey) for index-based access
    private List<string>? _entryKeys;

    // Page cache: sliding window ±2 around current page
    private readonly ConcurrentDictionary<int, BitmapSource> _cache = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public int TotalPages { get; private set; }

    public PageLoaderService(Manga manga)
    {
        _filePath = manga.FilePath;
        _ext = Path.GetExtension(_filePath).ToLowerInvariant();

        if (_ext == ".pdf")
            InitPdf();
        else
            InitArchive();
    }

    private void InitPdf()
    {
        _docReader = DocLib.Instance.GetDocReader(_filePath, new PageDimensions(1920, 2880));
        TotalPages = _docReader.GetPageCount();
    }

    private void InitArchive()
    {
        // Open once just to enumerate entry keys; re-open per-page to support RAR sequential reads
        using var archive = ArchiveFactory.OpenArchive(_filePath, new ReaderOptions());
        _entryKeys = archive.Entries
            .Where(e => !e.IsDirectory &&
                        ImageExtensions.Contains(
                            Path.GetExtension(e.Key ?? "").ToLowerInvariant()))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.Key!)
            .ToList();
        TotalPages = _entryKeys.Count;
    }

    // ── Public interface ──────────────────────────────────────────────────────

    public async Task<BitmapSource?> LoadPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= TotalPages) return null;
        if (_cache.TryGetValue(pageIndex, out var cached)) return cached;

        await _loadLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(pageIndex, out cached)) return cached;

            var bitmap = await Task.Run(() => RenderPage(pageIndex));
            if (bitmap is not null)
                _cache[pageIndex] = bitmap;
            return bitmap;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void Preload(int currentPage)
    {
        int min = currentPage - 1;
        int max = currentPage + 2;
        foreach (var key in _cache.Keys.ToArray())
            if (key < min || key > max)
                _cache.TryRemove(key, out _);

        for (int i = currentPage + 1; i <= Math.Min(currentPage + 2, TotalPages - 1); i++)
        {
            int page = i;
            if (!_cache.ContainsKey(page))
                _ = LoadPageAsync(page);
        }
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private BitmapSource? RenderPage(int pageIndex)
        => _ext == ".pdf" ? RenderPdfPage(pageIndex) : RenderArchivePage(pageIndex);

    private BitmapSource? RenderPdfPage(int pageIndex)
    {
        if (_docReader is null) return null;
        using var pageReader = _docReader.GetPageReader(pageIndex);
        int w   = pageReader.GetPageWidth();
        int h   = pageReader.GetPageHeight();
        byte[] raw = pageReader.GetImage();

        var bmp = BitmapSource.Create(w, h, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null, raw, w * 4);
        bmp.Freeze();
        return bmp;
    }

    private BitmapSource? RenderArchivePage(int pageIndex)
    {
        if (_entryKeys is null || pageIndex >= _entryKeys.Count) return null;
        var targetKey = _entryKeys[pageIndex];

        // Re-open archive for each page so RAR sequential access works correctly
        using var archive = ArchiveFactory.OpenArchive(_filePath, new ReaderOptions());
        var entry = archive.Entries.FirstOrDefault(e => e.Key == targetKey);
        if (entry is null) return null;

        using var ms = new MemoryStream();
        entry.WriteTo(ms);
        ms.Position = 0;

        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption  = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    public void Dispose()
    {
        _cache.Clear();
        _loadLock.Dispose();
        _docReader?.Dispose();
    }
}
