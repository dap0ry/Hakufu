using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Hakufu.Services;

// Compartido entre SyncViewModel (sube portadas sueltas) y BackupViewModel
// (sube portada + archivo completo a Drive) — un único sitio para el
// slug de nombres de colección/manga y la re-codificación a JPEG.
public static class CoverUploadHelper
{
    public static string Slugify(string text) =>
        Regex.Replace(text.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

    public static async Task<byte[]?> ToJpegAsync(BitmapSource bmp)
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return (byte[]?)ms.ToArray();
            }
            catch { return null; }
        });
    }
}
