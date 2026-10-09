using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Provider;
using Avalonia.Android;
using Hakufu.Services;

namespace Hakufu.Platforms.Droid;

/// <summary>
/// «Elegir foto» en Android: el selector de fotos del sistema (Android 13+; antes, el de
/// documentos con imágenes). No pide permiso para la galería: llega solo la foto elegida. Se
/// guarda como JPEG derecho (EXIF aplicado) de MaxSide px como mucho, reduciendo ya al leer:
/// una foto de 48 MP no cabe entera en memoria.
/// </summary>
internal static class PhotoPicker
{
    private const int RequestCode = 4201;
    private const int MaxSide = 2048;

    public static Task<string?> PickAsync(Activity activity)
    {
        if (activity is not AvaloniaActivity avalonia) return Task.FromResult<string?>(null);
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnResult(int code, Result result, Intent? data)
        {
            if (code != RequestCode) return;
            avalonia.ActivityResult -= OnResult;
            if (result != Result.Ok || data?.Data is not { } uri) { done.TrySetResult(null); return; }
            Task.Run(() =>
            {
                try { done.TrySetResult(SaveAsJpeg(activity, uri)); }
                catch { done.TrySetResult(null); } // no se pudo leer: como si se hubiera cancelado
            });
        }

        avalonia.ActivityResult += OnResult;
        var intent = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? new Intent(MediaStore.ActionPickImages).SetType("image/*")
            : new Intent(Intent.ActionOpenDocument).AddCategory(Intent.CategoryOpenable).SetType("image/*");
        try { activity.StartActivityForResult(intent, RequestCode); }
        catch (ActivityNotFoundException)
        {
            avalonia.ActivityResult -= OnResult;
            return Task.FromResult<string?>(null);
        }
        return done.Task;
    }

    private static string? SaveAsJpeg(Context ctx, global::Android.Net.Uri uri)
    {
        var resolver = ctx.ContentResolver!;

        // Primero solo el tamaño, para elegir cuánto reducir al decodificar.
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        using (var s = resolver.OpenInputStream(uri)) BitmapFactory.DecodeStream(s, null, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;
        var sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= MaxSide) sample *= 2;

        using var decoded = Decode(resolver, uri, new BitmapFactory.Options { InSampleSize = sample });
        if (decoded is null) return null;

        var matrix = new Matrix();
        var scale = Math.Min(1f, MaxSide / (float)Math.Max(decoded.Width, decoded.Height));
        matrix.PostScale(scale, scale);
        matrix.PostRotate(ExifRotation(resolver, uri));
        using var upright = Bitmap.CreateBitmap(decoded, 0, 0, decoded.Width, decoded.Height, matrix, true)!;

        Directory.CreateDirectory(FilePickerService.PickedDir);
        var path = FilePickerService.UniquePath(FilePickerService.PickedDir, "foto.jpg");
        using (var file = File.Create(path)) upright.Compress(Bitmap.CompressFormat.Jpeg!, 90, file);
        return path;
    }

    private static Bitmap? Decode(ContentResolver resolver, global::Android.Net.Uri uri, BitmapFactory.Options options)
    {
        using var s = resolver.OpenInputStream(uri);
        return BitmapFactory.DecodeStream(s, null, options);
    }

    private static int ExifRotation(ContentResolver resolver, global::Android.Net.Uri uri)
    {
        using var s = resolver.OpenInputStream(uri);
        if (s is null) return 0;
        return new ExifInterface(s).GetAttributeInt(ExifInterface.TagOrientation, 1) switch
        {
            6 => 90,
            3 => 180,
            8 => 270,
            _ => 0,
        };
    }
}
