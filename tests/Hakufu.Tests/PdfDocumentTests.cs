using Avalonia.Headless.XUnit;
using Hakufu.Services;

namespace Hakufu.Tests;

/// <summary>
/// PdfDocument: el PDF de cada plataforma (pdfium en escritorio, CoreGraphics en
/// iOS) con la misma forma de uso. Aquí se prueba la de escritorio.
/// </summary>
public class PdfDocumentTests
{
    [AvaloniaFact]
    public void Counts_pages_and_renders_within_the_requested_size()
    {
        using var tmp = new TempDataDir();
        var pdf = Fixtures.MakePdf(tmp.Root, "Tomo 1.pdf"); // 200×300 pt

        using var doc = PdfDocument.Open(pdf, 300, 450);

        Assert.Equal(1, doc.PageCount);
        using var page = doc.RenderPage(0);
        Assert.InRange(page.PixelSize.Width, 1, 300);
        Assert.InRange(page.PixelSize.Height, 1, 450);
    }

    [Fact]
    public void Corrupt_file_throws()
    {
        using var tmp = new TempDataDir();
        var bad = Path.Combine(tmp.Root, "roto.pdf");
        File.WriteAllText(bad, "esto no es un PDF");

        Assert.ThrowsAny<Exception>(() => PdfDocument.Open(bad, 300, 450).Dispose());
    }
}
