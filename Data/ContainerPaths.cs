using System.Text.RegularExpressions;

namespace Hakufu.Data;

/// <summary>
/// En iOS cada app vive en .../Containers/Data/Application/&lt;GUID&gt;/ y ese GUID
/// puede cambiar al reinstalarla (el sideload se repite cada 7 días). Las rutas
/// guardadas en data.json apuntan al contenedor viejo: se pasan al actual.
/// </summary>
public static partial class ContainerPaths
{
    [GeneratedRegex(@"^.*?/Containers/Data/Application/[0-9A-Fa-f-]{36}(?=/|$)")]
    private static partial Regex Container();

    /// <summary>
    /// Cambia el contenedor de todas las rutas guardadas por <paramref name="currentHome"/>
    /// (la raíz del contenedor actual). Las rutas que no son de un contenedor no se tocan.
    /// Devuelve si cambió algo (hay que guardar).
    /// </summary>
    public static bool Rebase(AppDataStore store, string currentHome)
    {
        currentHome = currentHome.TrimEnd('/');
        var changed = false;

        string Fix(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var match = Container().Match(path);
            if (!match.Success || match.Value == currentHome) return path;
            changed = true;
            return currentHome + path[match.Length..];
        }

        store.LibraryRoot = Fix(store.LibraryRoot);
        foreach (var m in store.Mangas)
        {
            m.FilePath       = Fix(m.FilePath);
            m.CoverCachePath = Fix(m.CoverCachePath);
        }
        store.Profile.AvatarPath = Fix(store.Profile.AvatarPath);
        return changed;
    }
}
