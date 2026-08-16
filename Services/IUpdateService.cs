namespace Hakufu.Services;

public interface IUpdateService
{
    /// Comprueba y descarga una actualización en segundo plano, sin
    /// avisar ni interrumpir al usuario. No lanza excepción si falla
    /// (sin red, no instalado vía Velopack, etc.) — falla en silencio.
    Task CheckForUpdatesInBackgroundAsync();

    /// True si ya hay una actualización descargada lista para aplicar.
    /// Lo recuerda Velopack entre reinicios de la app, no es solo
    /// memoria de este proceso.
    bool IsUpdateReadyToApply { get; }

    /// Aplica la actualización pendiente y reinicia la app. No hace
    /// nada si no hay ninguna actualización lista.
    void ApplyUpdateAndRestart();
}
