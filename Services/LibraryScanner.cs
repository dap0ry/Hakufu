using Hakufu.Data;
using Hakufu.I18n;
using Hakufu.MVVM.Model;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Hakufu.Services;

public enum ScanStatus { Ok, NoRoot, Unreadable }

public sealed record ScanResult(ScanStatus Status, string Root = "")
{
    public bool Ok => Status == ScanStatus.Ok;

    /// <summary>Mensaje para el usuario si no se pudo leer (null si fue bien).</summary>
    public string? Message => Status switch
    {
        ScanStatus.NoRoot     => L.Get("library.scan_no_root"),
        ScanStatus.Unreadable => L.Format("library.scan_unreadable", Root),
        _                     => null,
    };
}

/// <summary>
/// La biblioteca es una carpeta del usuario: cada subcarpeta es una colección
/// y sus .cbz/.cbr/.pdf son los tomos (los sueltos en la raíz van a "Sin
/// colección"). Hakufu solo lee esa carpeta: nunca copia, mueve ni borra nada
/// en ella. Este servicio sincroniza data.json con lo que hay en disco
/// conservando Ids, progreso y favoritos de lo que sigue ahí.
/// </summary>
public class LibraryScanner
{
    // Dato guardado (y usado para emparejar colecciones antiguas): no se traduce aquí,
    // sino al mostrarlo (LibraryService.DisplayName).
    public const string LooseCollectionName = "Sin colección";
    private static readonly HashSet<string> Extensions = new([".cbz", ".cbr", ".pdf"], StringComparer.OrdinalIgnoreCase);

    private readonly IDataRepository _repo;
    private Task<ScanResult>? _running;

    public LibraryScanner(IDataRepository repo) => _repo = repo;

    /// <summary>
    /// Carpeta de la biblioteca: la elegida, o si no hay ninguna la antigua
    /// &lt;datos&gt;/biblioteca donde Hakufu copiaba los mangas (si tiene algo),
    /// para no perder el progreso al actualizar. Null = hay que elegirla.
    /// </summary>
    public string? Root
    {
        get
        {
            if (AppPaths.FixedLibraryRoot is { } fixedRoot) return fixedRoot;
            if (_repo.Current.LibraryRoot is { Length: > 0 } root) return root;
            if (AppPaths.DefaultLibraryRoot is { } byDefault) return byDefault;
            return HasAnyFile(AppPaths.LibraryDir) ? AppPaths.LibraryDir : null;
        }
    }

    /// <summary>
    /// Lee la carpeta y actualiza la biblioteca. Nunca hay dos lecturas a la
    /// vez: si ya hay una en marcha se lee otra vez al acabar (los que piden
    /// mientras tanto comparten esa segunda lectura).
    /// </summary>
    public Task<ScanResult> ScanAsync()
    {
        if (_running is { IsCompleted: false } running) return _queued ??= AfterAsync(running);
        return _running = DoScanAsync();
    }

    private Task<ScanResult>? _queued;

    private async Task<ScanResult> AfterAsync(Task running)
    {
        await running;
        _queued = null;
        return await (_running = DoScanAsync());
    }

    /// <summary>Cambia la carpeta de la biblioteca y la lee.</summary>
    public async Task<ScanResult> SetRootAsync(string root)
    {
        _repo.Current.LibraryRoot = root;
        await _repo.SaveAsync();
        return await ScanAsync();
    }

    private async Task<ScanResult> DoScanAsync()
    {
        try { return await ScanCoreAsync(); }
        catch { return new(ScanStatus.Unreadable, Root ?? ""); } // nunca tumba la app
    }

    private async Task<ScanResult> ScanCoreAsync()
    {
        var root = Root;
        if (root is null) return new(ScanStatus.NoRoot);
        // Android sin permiso: se ven las carpetas pero no sus archivos. Leer así quitaría todos
        // los tomos y su progreso: como con un disco desconectado, no se toca nada.
        if (AppPlatform.HasLibraryAccess is { } hasAccess && !hasAccess()) return new(ScanStatus.Unreadable, root);
        // Android: la carpeta por defecto es de Hakufu y se crea vacía (al dar el permiso aún no
        // existe). Una carpeta elegida que falta no se crea: puede ser una tarjeta SD quitada.
        if (root == AppPaths.DefaultLibraryRoot)
            try { Directory.CreateDirectory(root); } catch { /* sin permiso: se verá como ilegible */ }

        // Recorrer el disco fuera del hilo de UI; los cambios en los datos, en
        // el hilo que llama (la UI puede estar enumerando las listas).
        var folder = await Task.Run(() => ReadFolder(root));
        // Carpeta no accesible (disco desconectado…): no se toca nada, para no
        // perder el progreso por un USB sin enchufar.
        if (folder is null) return new(ScanStatus.Unreadable, root);
        // Se eligió otra carpeta mientras se leía esta: ya la leerá la lectura siguiente.
        if (AppPaths.FixedLibraryRoot is null &&
            _repo.Current.LibraryRoot is { Length: > 0 } chosen && chosen != root) return new(ScanStatus.Ok, chosen);

        _repo.Current.LibraryRoot = root; // la antigua "biblioteca" queda fijada como carpeta
        Apply(_repo.Current, root, folder);
        await _repo.SaveAsync();

        // Tomos nuevos: contar sus páginas para el progreso (0 hasta saberlas).
        var pending = _repo.Current.Mangas.Where(m => m.TotalPages == 0).ToList();
        if (pending.Count > 0)
        {
            var paths  = pending.Select(m => m.FilePath).ToList();
            var counts = await Task.Run(() => paths.Select(CountPages).ToList());
            var changed = false;
            for (var i = 0; i < pending.Count; i++)
                if (counts[i] > 0) { pending[i].TotalPages = counts[i]; changed = true; }
            if (changed) await _repo.SaveAsync();
        }
        return new(ScanStatus.Ok, root);
    }

    // ── Disco ────────────────────────────────────────────────────────────────

    /// <summary>Lo que hay en la carpeta: una entrada por colección con las rutas relativas de sus tomos.</summary>
    internal sealed record FolderGroup(string RelativePath, string Name, IReadOnlyList<string> Files);

    /// <summary>Colecciones de la carpeta (un nivel), o null si no se puede leer.</summary>
    internal static List<FolderGroup>? ReadFolder(string root)
    {
        try
        {
            var dir = new DirectoryInfo(root);
            if (!dir.Exists) return null;

            var groups = dir.EnumerateDirectories()
                // "." = ocultas; "_" = datos o copias (p. ej. _Hakufu), no colecciones.
                .Where(d => !d.Name.StartsWith('.') && !d.Name.StartsWith('_'))
                .OrderBy(d => d.Name, NaturalComparer.Instance)
                .Select(d => new FolderGroup(d.Name, d.Name, Volumes(d, d.Name + "/")))
                .ToList();

            var loose = Volumes(dir, "");
            if (loose.Count > 0) groups.Add(new FolderGroup("", LooseCollectionName, loose));
            return groups;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> Volumes(DirectoryInfo dir, string prefix)
    {
        try
        {
            return dir.EnumerateFiles()
                .Where(f => !f.Name.StartsWith('.') && Extensions.Contains(f.Extension))
                .Select(f => f.Name)
                .OrderBy(n => n, NaturalComparer.Instance)
                .Select(n => prefix + n)
                .ToList();
        }
        catch
        {
            return []; // subcarpeta sin permiso: colección vacía
        }
    }

    private static bool HasAnyFile(string dir)
    {
        try { return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any(); }
        catch { return false; }
    }

    /// <summary>Páginas de un tomo, o 0 si no se puede leer.</summary>
    internal static int CountPages(string path)
    {
        try
        {
            if (!File.Exists(path)) return 0;
            if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                CoverService.PdfLock.Wait();
                try
                {
                    using var doc = PdfDocument.Open(path, 100, 150);
                    return doc.PageCount;
                }
                finally { CoverService.PdfLock.Release(); }
            }

            using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions());
            return archive.Entries.Count(e => !e.IsDirectory &&
                CoverService.ImageExtensions.Contains(Path.GetExtension(e.Key ?? "").ToLowerInvariant()));
        }
        catch
        {
            return 0;
        }
    }

    // ── Sincronizar datos ────────────────────────────────────────────────────

    /// <summary>
    /// Deja Collections/Mangas igual que la carpeta. Los tomos se reconocen por
    /// su ruta relativa (los de versiones antiguas, sin ella, por su FilePath
    /// dentro de la raíz) y las colecciones por su carpeta, su nombre o los
    /// tomos que tenían, así que Ids, progreso, favoritos y orden se conservan.
    /// </summary>
    internal static void Apply(AppDataStore store, string root, IReadOnlyList<FolderGroup> folder)
    {
        // Tomos ya conocidos por ruta relativa (sin distinguir mayúsculas; la
        // coincidencia exacta gana si hay dos que solo se diferencian en eso).
        var known = new Dictionary<string, List<Manga>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in store.Mangas)
        {
            var rel = m.RelativePath is { Length: > 0 } r ? r : RelativeTo(root, m.FilePath);
            if (rel is null) continue;
            if (!known.TryGetValue(rel, out var list)) known[rel] = list = [];
            list.Add(m);
        }

        Manga TakeManga(string rel)
        {
            if (known.TryGetValue(rel, out var list) && list.Count > 0)
            {
                var m = list.FirstOrDefault(x => (x.RelativePath is { Length: > 0 } r ? r : RelativeTo(root, x.FilePath)) == rel)
                        ?? list[0];
                list.Remove(m);
                return m;
            }
            return new Manga { DateAdded = DateTime.Now };
        }

        var oldCollections = store.Collections.ToList();
        Collection TakeCollection(FolderGroup g, IReadOnlyList<Manga> mangas)
        {
            var col = oldCollections.FirstOrDefault(c => c.RelativePath is not null &&
                                                         string.Equals(c.RelativePath, g.RelativePath, StringComparison.OrdinalIgnoreCase))
                   ?? oldCollections.FirstOrDefault(c => c.RelativePath is null &&
                                                         string.Equals(c.Name, g.Name, StringComparison.OrdinalIgnoreCase))
                   // Versiones antiguas: la carpeta copiada podía llamarse distinto
                   // ("Re:Zero" → "Re_Zero"); vale la colección que tenía sus tomos.
                   ?? oldCollections.FirstOrDefault(c => c.RelativePath is null &&
                                                         mangas.Any(m => c.MangaIds.Contains(m.Id)))
                   ?? new Collection();
            oldCollections.Remove(col);
            return col;
        }

        var collections = new List<Collection>();
        var mangasKept  = new List<Manga>();
        foreach (var g in folder)
        {
            var mangas = g.Files.Select(TakeManga).ToList();
            for (var i = 0; i < mangas.Count; i++)
            {
                var m = mangas[i];
                m.RelativePath = g.Files[i];
                m.FilePath     = Path.Combine([root, .. g.Files[i].Split('/')]);
                m.Title        = Path.GetFileNameWithoutExtension(g.Files[i]);
            }

            var col = TakeCollection(g, mangas);
            col.RelativePath = g.RelativePath;
            col.Name         = g.Name;
            col.MangaIds     = mangas.Select(m => m.Id).ToList();
            collections.Add(col);
            mangasKept.AddRange(mangas);
        }

        // Lo que ya no está en disco: fuera, con su progreso y su portada en caché.
        var keptIds = mangasKept.Select(m => m.Id).ToHashSet();
        foreach (var gone in store.Mangas.Where(m => !keptIds.Contains(m.Id)))
            try { File.Delete(Path.Combine(AppPaths.CoversDir, $"{gone.Id}.png")); } catch { /* da igual */ }
        store.Progress.RemoveAll(p => !keptIds.Contains(p.MangaId));
        store.Mangas      = mangasKept;
        store.Collections = collections;
    }

    /// <summary>Ruta relativa con '/' si path está dentro de root; si no, null.</summary>
    private static string? RelativeTo(string root, string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var r = root.Replace('\\', '/').TrimEnd('/') + "/";
        var p = path.Replace('\\', '/');
        return p.StartsWith(r, StringComparison.OrdinalIgnoreCase) && p.Length > r.Length ? p[r.Length..] : null;
    }
}
