using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Hakufu.Services;

/// <summary>Conversión de los datos que dan pdfium y los archivos a Bitmap de Avalonia.</summary>
internal static class BitmapHelper
{
    /// <summary>pdfium (Docnet) devuelve BGRA sin premultiplicar, 4 bytes por píxel.</summary>
    public static Bitmap FromBgra(byte[] raw, int width, int height)
        => FromPixels(raw, width, height, PixelFormat.Bgra8888, AlphaFormat.Unpremul);

    /// <summary>
    /// Android (PdfRenderer): RGBA premultiplicado, el orden nativo de Skia allí. En BGRA la GPU de
    /// algunos Android no sube la textura y la página sale en blanco.
    /// </summary>
    public static Bitmap FromRgba(byte[] raw, int width, int height)
        => FromPixels(raw, width, height, PixelFormat.Rgba8888, AlphaFormat.Premul);

    private static Bitmap FromPixels(byte[] raw, int width, int height, PixelFormat format, AlphaFormat alpha)
    {
        var bmp = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), format, alpha);
        using var fb = bmp.Lock();
        var rowBytes = width * 4;
        if (fb.RowBytes == rowBytes)
        {
            Marshal.Copy(raw, 0, fb.Address, raw.Length);
        }
        else
        {
            for (var y = 0; y < height; y++)
                Marshal.Copy(raw, y * rowBytes, fb.Address + y * fb.RowBytes, rowBytes);
        }
        return bmp;
    }

    /// <summary>Decodifica una imagen (jpg/png/webp…); con decodeWidth la reduce al vuelo.</summary>
    public static Bitmap FromStream(Stream stream, int? decodeWidth = null) =>
        decodeWidth is int w
            ? Bitmap.DecodeToWidth(stream, w, BitmapInterpolationMode.HighQuality)
            : new Bitmap(stream);

    public static Bitmap? TryLoad(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            // Se lee a memoria para no dejar el fichero bloqueado (en Windows
            // impediría sustituir la foto de perfil más tarde).
            using var ms = new MemoryStream(File.ReadAllBytes(path));
            return new Bitmap(ms);
        }
        catch { return null; }
    }
}
