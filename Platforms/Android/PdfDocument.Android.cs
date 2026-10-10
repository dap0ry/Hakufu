using System.Runtime.InteropServices;
using Android.Graphics.Pdf;
using Android.OS;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using DroidBitmap = Android.Graphics.Bitmap;

namespace Hakufu.Services;

/// <summary>
/// Un PDF abierto para contar y pintar sus páginas. Android: PdfRenderer del sistema (Docnet no
/// tiene binarios para Android). Misma forma que Services/PdfDocument.Docnet.cs. No es seguro
/// entre hilos: los servicios ya lo usan bajo su PdfLock.
/// </summary>
internal sealed class PdfDocument : IDisposable
{
    private readonly ParcelFileDescriptor _file;
    private readonly PdfRenderer _renderer;
    private readonly int _maxWidth, _maxHeight;

    private PdfDocument(ParcelFileDescriptor file, PdfRenderer renderer, int maxWidth, int maxHeight)
    {
        _file = file;
        _renderer = renderer;
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
    }

    /// <summary>Abre el PDF; las páginas se pintan para caber en maxWidth × maxHeight. Lanza si está dañado o con contraseña.</summary>
    public static PdfDocument Open(string path, int maxWidth, int maxHeight)
    {
        var file = ParcelFileDescriptor.Open(new Java.IO.File(path), ParcelFileMode.ReadOnly)
                   ?? throw new InvalidDataException($"No se puede abrir el PDF: {path}");
        try { return new(file, new PdfRenderer(file), maxWidth, maxHeight); }
        catch { file.Close(); throw; }
    }

    public int PageCount => _renderer.PageCount;

    public Bitmap RenderPage(int index)
    {
        // PdfRenderer solo deja una página abierta, y Dispose no la cierra en Java: Close a mano.
        using var page = _renderer.OpenPage(index);
        try { return Render(page); }
        finally { page.Close(); }
    }

    private Bitmap Render(PdfRenderer.Page page)
    {
        // En puntos y ya girado según /Rotate.
        double pageW = page.Width, pageH = page.Height;
        if (pageW <= 0 || pageH <= 0) throw new InvalidDataException("Página sin tamaño");

        // Cabe en el máximo, hacia arriba o hacia abajo (como Docnet con PageDimensions).
        var scale = Math.Min(_maxWidth / pageW, _maxHeight / pageH);
        var w = Math.Max(1, (int)Math.Round(pageW * scale));
        var h = Math.Max(1, (int)Math.Round(pageH * scale));

        using var bitmap = DroidBitmap.CreateBitmap(w, h, DroidBitmap.Config.Argb8888!)!;
        bitmap.EraseColor(global::Android.Graphics.Color.White); // papel blanco: PdfRenderer pinta sobre transparente
        page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);

        // ARGB_8888 queda en memoria como R,G,B,A premultiplicado: tal cual (ver BitmapHelper.FromRgba).
        var pixels = new byte[w * h * 4];
        using (var buffer = Java.Nio.ByteBuffer.AllocateDirect(pixels.Length)!)
        {
            bitmap.CopyPixelsToBuffer(buffer);
            Marshal.Copy(buffer.GetDirectBufferAddress(), pixels, 0, pixels.Length);
        }
        return BitmapHelper.FromRgba(pixels, w, h);
    }

    public void Dispose()
    {
        _renderer.Close();
        _file.Close();
        _renderer.Dispose();
        _file.Dispose();
    }
}
