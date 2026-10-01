namespace Hakufu.Services;

public interface IBackupService
{
    /// <summary>
    /// Crea un .zip con data.json, portadas y foto de perfil. Los mangas no
    /// van: son la carpeta de la biblioteca del usuario.
    /// </summary>
    Task ExportAsync(string zipPath, IProgress<double>? progress = null);

    /// <summary>
    /// Restaura una copia. Devuelve false, sin tocar nada, si el zip no es una
    /// copia de Hakufu válida. Las rutas guardadas se reescriben para la
    /// carpeta de datos de este equipo (sirve para pasar de Windows a Mac/Linux);
    /// la carpeta de la biblioteca sigue siendo la de este equipo.
    /// </summary>
    Task<bool> ImportAsync(string zipPath, IProgress<double>? progress = null);
}
