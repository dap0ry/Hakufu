namespace Hakufu.Services;

/// <summary>
/// Rutas de Android. El selector de carpetas del sistema devuelve una URI
/// (content://com.android.externalstorage.documents/tree/primary%3AMangas), pero con el
/// permiso de acceso a todos los archivos la misma carpeta se lee por su ruta. Aquí y no en
/// Platforms/Android para poder probarlo en Linux.
/// </summary>
public static class AndroidStorage
{
    public const string InternalStorage = "/storage/emulated/0";
    private const string ExternalStorageAuthority = "com.android.externalstorage.documents";

    /// <summary>Ruta de la carpeta de una URI de árbol, o null si no es del almacenamiento (Drive…).</summary>
    public static string? TreeUriToPath(Uri uri)
    {
        if (uri.IsFile) return uri.LocalPath;
        if (uri.Scheme != "content") return null;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tree = Array.IndexOf(segments, "tree");
        if (tree < 0 || tree + 1 >= segments.Length) return null;
        // "primary:Mangas", "1A2B-3C4D:Comics" o "raw:/storage/emulated/0/Download/Mangas"
        var id = Uri.UnescapeDataString(segments[tree + 1]);
        if (id.StartsWith("raw:", StringComparison.Ordinal)) return id[4..];
        if (uri.Host != ExternalStorageAuthority) return null;
        var colon = id.IndexOf(':');
        if (colon < 0) return null;
        var volume = id[..colon];
        var relative = id[(colon + 1)..].Trim('/');
        var root = volume == "primary" ? InternalStorage : $"/storage/{volume}";
        return relative.Length == 0 ? root : $"{root}/{relative}";
    }
}
