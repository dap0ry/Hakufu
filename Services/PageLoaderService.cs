using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Hakufu.MVVM.Model;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Hakufu.Services;

public class PageLoaderService : IPageLoaderService
{
    private readonly string _ext;
    private readonly string _filePath;

    // PDF-specific
    private PdfDocument? _pdf;

    // CBR/CBZ-specific — sorted list of (entryKey) for index-based access
    private List<string>? _entryKeys;

    // Page cache: sliding window around current page
    private readonly ConcurrentDictionary<int, Bitmap> _cache = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public int TotalPages { get; private set; }

    public PageLoaderService(Manga manga)
    {
        _filePath = manga.FilePath;
        _ext = Path.GetExtension(_filePath).ToLowerInvariant();

        // Un manga movido o borrado, o un archivo dañado, se abre con 0 páginas
        // en vez de tumbar la app.
        if (!File.Exists(_filePath)) return;
        try
        {
            if (_ext == ".pdf")
                InitPdf();
            else
                InitArchive();
        }
        catch
        {
            TotalPages = 0;
        }
    }

    private void InitPdf()
    {
        _pdf = PdfDocument.Open(_filePath, 1920, 2880);
        TotalPages = _pdf.PageCount;
    }

    private void InitArchive()
    {
        // Open once just to enumerate entry keys; re-open per-page to support RAR sequential reads
        using var archive = ArchiveFactory.OpenArchive(_filePath, new ReaderOptions());
        _entryKeys = archive.Entries
            .Where(e => !e.IsDirectory &&
                        CoverService.ImageExtensions.Contains(
                            Path.GetExtension(e.Key ?? "").ToLowerInvariant()))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.Key!)
            .ToList();
        TotalPages = _entryKeys.Count;
    }

    // ── Public interface ──────────────────────────────────────────────────────

    public async Task<Bitmap?> LoadPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= TotalPages) return null;
        if (_cache.TryGetValue(pageIndex, out var cached)) return cached;

        await _loadLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(pageIndex, out cached)) return cached;

            var bitmap = await Task.Run(() =>
            {
                try { return RenderPage(pageIndex); }
                catch { return null; }
            });
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

    private Bitmap? RenderPage(int pageIndex)
        => _ext == ".pdf" ? RenderPdfPage(pageIndex) : RenderArchivePage(pageIndex);

    private Bitmap? RenderPdfPage(int pageIndex)
    {
        return _pdf?.RenderPage(pageIndex);
    }

    private Bitmap? RenderArchivePage(int pageIndex)
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
        return BitmapHelper.FromStream(ms);
    }

    public void Dispose()
    {
        _cache.Clear();
        _loadLock.Dispose();
        _pdf?.Dispose();
    }
}
