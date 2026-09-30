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
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hakufu");

    public static string DataFile         => Path.Combine(DataDir, "data.json");
    public static string CoversDir        => Path.Combine(DataDir, "covers");
    public static string CustomizationDir => Path.Combine(DataDir, "customization");
    public static string LibraryDir       => Path.Combine(DataDir, "biblioteca");
}
