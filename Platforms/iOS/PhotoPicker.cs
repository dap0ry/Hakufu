using CoreGraphics;
using Foundation;
using Hakufu.Services;
using PhotosUI;
using UIKit;

namespace Hakufu.Platforms.iOS;

/// <summary>
/// «Elegir foto» en iPhone/iPad: el selector de Fotos del sistema (PHPicker). No
/// pide permiso para la galería: a la app solo llega la foto elegida. Se guarda
/// como JPEG, derecha y de <see cref="MaxSide"/> px como mucho: las fotos del
/// iPhone suelen ser HEIC (Skia no las lee), el giro va aparte (EXIF) y las de
/// 48 MP pesan demasiado para un avatar.
/// </summary>
internal static class PhotoPicker
{
    private const double MaxSide = 2048;

    // El delegado del selector es una referencia débil: se guarda aquí mientras está abierto.
    private static PickerDelegate? _open;

    public static Task<string?> PickAsync()
    {
        if (TopViewController() is not { } top) return Task.FromResult<string?>(null);

        var picker = new PHPickerViewController(new PHPickerConfiguration
        {
            SelectionLimit = 1,
            Filter         = PHPickerFilter.ImagesFilter
        });
        var done = new PickerDelegate();
        _open = done;
        picker.Delegate = done;
        // Cerrar deslizando hacia abajo también es cancelar.
        if (picker.PresentationController is { } presentation) presentation.Delegate = done;
        top.PresentViewController(picker, true, null);
        return done.Result.Task;
    }

    private static UIViewController? TopViewController()
    {
        var window = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(s => s.Windows)
            .FirstOrDefault(w => w.IsKeyWindow);
        var vc = window?.RootViewController;
        while (vc?.PresentedViewController is { } presented) vc = presented;
        return vc;
    }

    private sealed class PickerDelegate : NSObject, IPHPickerViewControllerDelegate, IUIAdaptivePresentationControllerDelegate
    {
        public TaskCompletionSource<string?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void DidFinishPicking(PHPickerViewController picker, PHPickerResult[] results)
        {
            picker.DismissViewController(true, null);
            if (results.Length == 0) { Finish(null); return; }
            // Puede tardar (si la foto está en iCloud, se descarga): llega en otro hilo.
            results[0].ItemProvider.LoadDataRepresentation("public.image", (data, error) =>
            {
                string? path = null;
                try { if (data is not null && error is null) path = SaveAsJpeg(data); }
                catch { /* no se pudo leer: como si se hubiera cancelado */ }
                Finish(path);
            });
        }

        [Export("presentationControllerDidDismiss:")]
        public void DidDismiss(UIPresentationController presentationController) => Finish(null);

        private void Finish(string? path)
        {
            if (ReferenceEquals(_open, this)) _open = null;
            Result.TrySetResult(path);
        }
    }

    private static string? SaveAsJpeg(NSData data)
    {
        using var image = UIImage.LoadFromData(data);
        if (image is null) return null;

        // Size va ya girado según la orientación; Draw la aplica y la foto sale derecha.
        var width  = image.Size.Width * image.CurrentScale;
        var height = image.Size.Height * image.CurrentScale;
        var scale  = Math.Min(1, MaxSide / Math.Max(width, height));
        var size   = new CGSize(Math.Round(width * scale), Math.Round(height * scale));
        var format = new UIGraphicsImageRendererFormat { Scale = 1 };
        using var renderer = new UIGraphicsImageRenderer(size, format);
        using var upright  = renderer.CreateImage(_ => image.Draw(new CGRect(CGPoint.Empty, size)));
        using var jpeg     = upright.AsJPEG(0.9f);
        if (jpeg is null) return null;

        Directory.CreateDirectory(FilePickerService.PickedDir);
        var path = FilePickerService.UniquePath(FilePickerService.PickedDir, "foto.jpg");
        File.WriteAllBytes(path, jpeg.ToArray());
        return path;
    }
}
