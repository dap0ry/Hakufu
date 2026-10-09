using Avalonia.Media.Imaging;

namespace Hakufu.Services;

/// <summary>Provisional (Task 5 del plan de Android): la misma forma que Services/PdfDocument.Docnet.cs.</summary>
internal sealed class PdfDocument : IDisposable
{
    public static PdfDocument Open(string path, int maxWidth, int maxHeight)
        => throw new NotSupportedException("PDF en Android: pendiente");

    public int PageCount => 0;

    public Bitmap RenderPage(int index) => throw new NotSupportedException();

    public void Dispose() { }
}
