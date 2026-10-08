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

    public static string DataFile         => Path.Combine(DataDir, "data.json");
    public static string CoversDir        => Path.Combine(DataDir, "covers");
    public static string ProfileDir       => Path.Combine(DataDir, "profile");
    // Donde hasta ahora Hakufu copiaba los mangas. Ya no se copia nada: solo se
    // usa como carpeta de la biblioteca por defecto si existe (ver LibraryScanner).
    public static string LibraryDir       => Path.Combine(DataDir, "biblioteca");
}
