namespace Hakufu.Services;

/// <summary>Qué va en la copia: el perfil siempre; de las colecciones, las elegidas.</summary>
public sealed record BackupOptions(IReadOnlyCollection<Guid> CollectionIds)
{
    public static readonly BackupOptions ProfileOnly = new(Array.Empty<Guid>());
}

/// <summary>Resultado de restaurar: cuántos tomos de la copia se encontraron en la carpeta de este equipo.</summary>
public sealed record ImportResult(bool Ok, int Applied = 0, int Missing = 0, bool Legacy = false)
{
    public static readonly ImportResult Invalid = new(false);
}

public interface IBackupService
{
    /// <summary>
    /// Crea un .zip con el perfil (nombre, foto, estadísticas y ajustes) y,
    /// de las colecciones elegidas, el progreso, favoritos, orden e historial
    /// de sus tomos. Ni mangas (son la carpeta del usuario) ni portadas (se
    /// regeneran solas). Se queda donde el usuario lo guarde: no se sube a nada.
    /// </summary>
    Task ExportAsync(string zipPath, BackupOptions options, IProgress<double>? progress = null);

    /// <summary>
    /// Restaura una copia combinándola con lo que hay: aplica el perfil y, a
    /// los tomos de la carpeta de la biblioteca de este equipo que coincidan
    /// (misma subcarpeta y nombre), su progreso y favoritos. No borra nada que
    /// no esté en la copia. Las copias antiguas (completas) sustituyen la
    /// biblioteca como antes. Devuelve <see cref="ImportResult.Invalid"/>, sin
    /// tocar nada, si el zip no es una copia de Hakufu.
    /// </summary>
    Task<ImportResult> ImportAsync(string zipPath, IProgress<double>? progress = null);
}
