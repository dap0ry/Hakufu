namespace Hakufu.Services;

public enum UpdateCheckStatus { UpToDate, Available, Failed }

/// <param name="Version">La versión nueva, si la hay ("0.11.0").</param>
/// <param name="CanSelfUpdate">Instalada con Velopack: se puede descargar y aplicar desde la app.</param>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Version = null, bool CanSelfUpdate = false);

/// <summary>Comprueba, descarga y aplica versiones nuevas. Es lo único de Hakufu que usa la red.</summary>
public interface IUpdateService
{
    /// <summary>Versión que está corriendo ("0.10.1").</summary>
    string CurrentVersion { get; }

    /// <summary>Nunca lanza: sin conexión o con cualquier error devuelve Failed.</summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);

    /// <summary>Descarga la versión encontrada por CheckAsync. Lanza si falla.</summary>
    Task DownloadAsync(Action<int> progress, CancellationToken ct = default);

    /// <summary>Sale de la app, aplica la actualización y la vuelve a abrir.</summary>
    void ApplyAndRestart();
}
