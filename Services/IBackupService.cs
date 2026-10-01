namespace Hakufu.Services;

public interface IBackupService
{
    /// <summary>
    /// Crea un .zip con data.json, portadas y foto de perfil. Con
    /// includeLibraryFiles también mete los mangas copiados a la carpeta
    /// "biblioteca" de Hakufu (los que están fuera de ella nunca se incluyen).
    /// </summary>
    Task ExportAsync(string zipPath, bool includeLibraryFiles, IProgress<double>? progress = null);

    /// <summary>
    /// Restaura una copia. Devuelve false, sin tocar nada, si el zip no es una
    /// copia de Hakufu válida. Las rutas guardadas se reescriben para la
    /// carpeta de datos de este equipo (sirve para pasar de Windows a Mac/Linux).
    /// </summary>
    Task<bool> ImportAsync(string zipPath, IProgress<double>? progress = null);
}
