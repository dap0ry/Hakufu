using Avalonia.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;

namespace Hakufu.Services;

/// <summary>
/// Un PDF abierto para contar y pintar sus páginas. Escritorio: pdfium (Docnet).
/// En iOS, Docnet no tiene binarios: allí está Platforms/iOS/PdfDocument.iOS.cs,
/// con la misma forma. pdfium no es seguro entre hilos: quien lo use a la vez
/// desde varios sitios se coordina con CoverService.PdfLock.
/// </summary>
internal sealed class PdfDocument : IDisposable
{
    private readonly IDocReader _reader;

    private PdfDocument(IDocReader reader) => _reader = reader;

    /// <summary>Abre el PDF; las páginas se pintan para caber en maxWidth × maxHeight. Lanza si está dañado.</summary>
    public static PdfDocument Open(string path, int maxWidth, int maxHeight)
        => new(DocLib.Instance.GetDocReader(path, new PageDimensions(maxWidth, maxHeight)));

    public int PageCount => _reader.GetPageCount();

    public Bitmap RenderPage(int index)
    {
        using var page = _reader.GetPageReader(index);
        return BitmapHelper.FromBgra(page.GetImage(), page.GetPageWidth(), page.GetPageHeight());
    }

    public void Dispose() => _reader.Dispose();
}
