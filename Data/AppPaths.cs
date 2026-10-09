namespace Hakufu.Data;

/// <summary>
/// Carpetas de datos de Hakufu. Windows: %APPDATA%\Hakufu (igual que la 0.9.x,
/// así se conserva la biblioteca). Linux/macOS: ~/.config/Hakufu.
/// HAKUFU_DATA_DIR la sustituye (tests, o tener una biblioteca de pruebas).
/// </summary>
public static class AppPaths
{
    public static string DataDir =>
        Environment.GetEnvironmentVariable("HAKUFU_DATA_DIR") is { Length: > 0 } overrideDir
            ? overrideDir
            : DataDirOverride
              ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu");

    /// <summary>
    /// iOS: Library/Application Support/Hakufu (lo fija Platforms/iOS/AppDelegate). Allí
    /// ApplicationData cae dentro de Documentos, que es la biblioteca visible en Archivos.
    /// </summary>
    public static string? DataDirOverride { get; set; }

    /// <summary>
    /// iOS: la carpeta Documentos de la app («En mi iPhone → Hakufu» en Archivos) es
    /// siempre la biblioteca. Null en escritorio: la elige el usuario.
    /// </summary>
    public static string? FixedLibraryRoot { get; set; }

    /// <summary>Android: la biblioteca mientras no se elija otra (Hakufu en el almacenamiento interno). Null en el resto.</summary>
    public static string? DefaultLibraryRoot { get; set; }

    /// <summary>Android: dónde se guarda sin selector (Descargas). iOS usa la carpeta de Hakufu en Archivos.</summary>
    public static string? SaveDirOverride { get; set; }

    /// <summary>Dónde se guarda sin selector en móvil (copia de seguridad, PNG del perfil); null en escritorio.</summary>
    public static string? SaveDir => SaveDirOverride ?? FixedLibraryRoot;

    /// <summary>
    /// Android: la carpeta que el usuario reconoce y la clave de su nombre («Almacenamiento interno»).
    /// iOS usa FixedLibraryRoot con «Archivos → En mi iPhone/iPad → Hakufu».
    /// </summary>
    public static (string Path, string NameKey)? VisibleRoot { get; set; }

    public static string DataFile         => Path.Combine(DataDir, "data.json");
    public static string CoversDir        => Path.Combine(DataDir, "covers");
    public static string ProfileDir       => Path.Combine(DataDir, "profile");
    // Donde hasta ahora Hakufu copiaba los mangas. Ya no se copia nada: solo se
    // usa como carpeta de la biblioteca por defecto si existe (ver LibraryScanner).
    public static string LibraryDir       => Path.Combine(DataDir, "biblioteca");
}
