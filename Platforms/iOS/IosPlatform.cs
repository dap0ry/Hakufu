using Foundation;
using Hakufu.Data;
using Hakufu.Services;
using UIKit;

namespace Hakufu.Platforms.iOS;

/// <summary>Lo que cambia en iPhone/iPad (ver AppPlatform y AppPaths).</summary>
internal static class IosPlatform
{
    public static void Configure()
    {
        var files = NSFileManager.DefaultManager;
        string Folder(NSSearchPathDirectory dir) => files.GetUrls(dir, NSSearchPathDomain.User)[0].Path!;

        // Documentos = «En mi iPhone → Hakufu» en la app Archivos (UIFileSharingEnabled y
        // LSSupportsOpeningDocumentsInPlace en Info.plist): es la biblioteca.
        AppPaths.FixedLibraryRoot = Folder(NSSearchPathDirectory.DocumentDirectory);
        // Los datos de Hakufu, fuera de la vista: Library/Application Support/Hakufu.
        AppPaths.DataDirOverride = Path.Combine(Folder(NSSearchPathDirectory.ApplicationSupportDirectory), "Hakufu");

        AppPlatform.IsMobile = true;
        AppPlatform.OpenFolder = OpenInFiles;
        AppPlatform.OpenUrl = url => Open(new NSUrl(url));
        AppPlatform.PickPhotoAsync = PhotoPicker.PickAsync;
    }

    // shareddocuments:// abre la app Archivos en esa carpeta.
    private static void OpenInFiles(string path)
    {
        var url = new NSUrlComponents { Scheme = "shareddocuments", Path = path }.Url;
        if (url is not null) Open(url);
    }

    private static void Open(NSUrl url)
        => UIApplication.SharedApplication.OpenUrl(url, new UIApplicationOpenUrlOptions(), null);
}
