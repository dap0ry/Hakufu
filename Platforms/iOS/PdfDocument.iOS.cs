using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using CoreGraphics;

namespace Hakufu.Services;

/// <summary>
/// Un PDF abierto para contar y pintar sus páginas. iOS: CoreGraphics de Apple (Docnet
/// no tiene binarios para iOS). Misma forma que Services/PdfDocument.Docnet.cs.
/// </summary>
internal sealed class PdfDocument : IDisposable
{
    private readonly CGPDFDocument _doc;
    private readonly int _maxWidth, _maxHeight;

    private PdfDocument(CGPDFDocument doc, int maxWidth, int maxHeight)
    {
        _doc = doc;
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
    }

    /// <summary>Abre el PDF; las páginas se pintan para caber en maxWidth × maxHeight. Lanza si está dañado.</summary>
    public static PdfDocument Open(string path, int maxWidth, int maxHeight)
    {
        var doc = CGPDFDocument.FromFile(path) ?? throw new InvalidDataException($"No se puede abrir el PDF: {path}");
        if (doc.IsEncrypted && !doc.IsUnlocked)
        {
            doc.Dispose();
            throw new InvalidDataException($"PDF con contraseña: {path}");
        }
        return new(doc, maxWidth, maxHeight);
    }

    public int PageCount => (int)_doc.Pages;

    public Bitmap RenderPage(int index)
    {
        using var page = _doc.GetPage(index + 1) ?? throw new ArgumentOutOfRangeException(nameof(index));
        var box = page.GetBoxRect(CGPDFBox.Crop);
        var rotation = ((page.RotationAngle % 360) + 360) % 360;
        var sideways = rotation is 90 or 270;
        double pageW = sideways ? box.Height : box.Width, pageH = sideways ? box.Width : box.Height;
        if (pageW <= 0 || pageH <= 0) throw new InvalidDataException("Página sin tamaño");

        // Cabe en el máximo, hacia arriba o hacia abajo (como Docnet con PageDimensions).
        var scale = Math.Min(_maxWidth / pageW, _maxHeight / pageH);
        var w = Math.Max(1, (int)Math.Round(pageW * scale));
        var h = Math.Max(1, (int)Math.Round(pageH * scale));

        var stride = w * 4;
        var pixels = new byte[stride * h];
        var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var colors = CGColorSpace.CreateDeviceRGB();
            // BGRA en memoria (little-endian + alfa primero), igual que pdfium.
            using var ctx = new CGBitmapContext(pin.AddrOfPinnedObject(), w, h, 8, stride, colors,
                CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little);
            ctx.SetFillColor(1, 1, 1, 1); // papel blanco: todo opaco
            ctx.FillRect(new CGRect(0, 0, w, h));
            ctx.InterpolationQuality = CGInterpolationQuality.High;

            // CoreGraphics aplica estas operaciones al revés de como se escriben:
            // primero se lleva la página a su origen, se escala, se gira y se coloca.
            switch (rotation)
            {
                case 90:  ctx.TranslateCTM(0, h); ctx.RotateCTM((nfloat)(-Math.PI / 2)); break;
                case 180: ctx.TranslateCTM(w, h); ctx.RotateCTM((nfloat)Math.PI); break;
                case 270: ctx.TranslateCTM(w, 0); ctx.RotateCTM((nfloat)(Math.PI / 2)); break;
            }
            ctx.ScaleCTM((nfloat)scale, (nfloat)scale);
            ctx.TranslateCTM(-box.X, -box.Y);
            ctx.DrawPDFPage(page);
        }
        finally
        {
            pin.Free();
        }
        return BitmapHelper.FromBgra(pixels, w, h);
    }

    public void Dispose() => _doc.Dispose();
}
