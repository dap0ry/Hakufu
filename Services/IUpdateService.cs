using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public interface IUpdateService
{
    Version GetCurrentVersion();

    /// Se usa solo para mostrar el changelog en pantalla — independiente
    /// de cómo se descarga/aplica la actualización real (eso lo gestiona
    /// Velopack por debajo).
    Task<GitHubRelease?> FetchLatestReleaseAsync();

    /// Comprueba y descarga una actualización en segundo plano, sin
    /// avisar ni interrumpir al usuario. No lanza excepción si falla
    /// (sin red, no instalado vía Velopack, etc.) — falla en silencio.
    Task CheckForUpdatesInBackgroundAsync();

    /// True una vez hay una actualización descargada lista para aplicar.
    bool IsUpdateReadyToApply { get; }

    /// Aplica la actualización pendiente y reinicia la app. No hace
    /// nada si no hay ninguna actualización lista.
    void ApplyUpdateAndRestart();
}
