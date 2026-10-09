using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Hakufu.Data;
using Hakufu.I18n;
using Hakufu.Services;

namespace Hakufu.Platforms.Droid;

/// <summary>Lo que cambia en Android (ver AppPlatform y AppPaths).</summary>
internal static class DroidPlatform
{
    public static void Configure(Activity activity)
    {
        var ctx = activity.ApplicationContext!;
        // Los datos de Hakufu, privados de la app.
        AppPaths.DataDirOverride = Path.Combine(ctx.FilesDir!.AbsolutePath, "Hakufu");
        // La biblioteca, mientras no se elija otra: «Hakufu» en el almacenamiento interno.
        AppPaths.DefaultLibraryRoot = Path.Combine(AndroidStorage.InternalStorage, "Hakufu");
        AppPaths.SaveDirOverride = Path.Combine(AndroidStorage.InternalStorage, global::Android.OS.Environment.DirectoryDownloads!);
        AppPaths.VisibleRoot = (AndroidStorage.InternalStorage, "files.android_storage");
        Localizer.TextsDirOverride = ExtractTexts(ctx);

        AppPlatform.IsMobile = true;
        AppPlatform.HasLibraryAccess = HasAllFilesAccess;
        AppPlatform.RequestLibraryAccess = () => RequestAllFilesAccess(activity);
        AppPlatform.OpenUrl = url => TryStart(activity, new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url)));
        AppPlatform.OpenFolder = path => OpenFolder(activity, path);
        AppPlatform.PickPhotoAsync = () => PhotoPicker.PickAsync(activity);
    }

    // Los textos van en el APK (assets/i18n): se copian a una carpeta para el Localizer de siempre.
    private static string ExtractTexts(Context ctx)
    {
        var dir = Path.Combine(ctx.CacheDir!.AbsolutePath, "i18n");
        Directory.CreateDirectory(dir);
        foreach (var name in ctx.Assets!.List("i18n") ?? [])
        {
            using var src = ctx.Assets.Open($"i18n/{name}");
            using var dst = File.Create(Path.Combine(dir, name));
            src.CopyTo(dst);
        }
        return dir;
    }

    // La carpeta por defecto la crea LibraryScanner al leerla.
    private static bool HasAllFilesAccess()
        => OperatingSystem.IsAndroidVersionAtLeast(30)
            ? global::Android.OS.Environment.IsExternalStorageManager
            : Application.Context.CheckSelfPermission(Manifest.Permission.ReadExternalStorage) == Permission.Granted;

    private static void RequestAllFilesAccess(Activity activity)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var app = new Intent(Settings.ActionManageAppAllFilesAccessPermission,
                                 global::Android.Net.Uri.Parse("package:" + activity.PackageName));
            if (!TryStart(activity, app)) TryStart(activity, new Intent(Settings.ActionManageAllFilesAccessPermission));
        }
        else
            activity.RequestPermissions([Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage], 1);
    }

    // El gestor de archivos del sistema en esa carpeta; si el móvil no sabe, nada.
    private static void OpenFolder(Activity activity, string path)
    {
        if (!path.StartsWith(AndroidStorage.InternalStorage, StringComparison.Ordinal)) return;
        var docId = "primary:" + path[AndroidStorage.InternalStorage.Length..].TrimStart('/');
        var uri = DocumentsContract.BuildDocumentUri("com.android.externalstorage.documents", docId);
        var intent = new Intent(Intent.ActionView).SetDataAndType(uri, DocumentsContract.Document.MimeTypeDir)
                                                  .AddFlags(ActivityFlags.GrantReadUriPermission);
        TryStart(activity, intent);
    }

    private static bool TryStart(Activity activity, Intent intent)
    {
        try { activity.StartActivity(intent); return true; }
        catch (ActivityNotFoundException) { return false; }
    }
}
