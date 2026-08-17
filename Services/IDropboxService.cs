namespace Hakufu.Services;

public interface IDropboxService
{
    Task<bool> IsConnectedAsync();

    // Pide un link de conexión al backend y lo abre en el navegador del sistema.
    Task<string> StartConnectFlowAsync();

    Task DisconnectAsync();

    // Access token de Dropbox de corta duración, listo para llamar a
    // dropboxapi.com directamente.
    Task<string> GetAccessTokenAsync();

    // path es la ruta completa dentro de la carpeta de la app, ej.
    // "/One Piece/vol-01.cbz" — Dropbox crea las carpetas intermedias que
    // falten él solo, no hace falta buscarlas/crearlas antes (a diferencia
    // de Drive, que direcciona por ID en vez de por ruta).
    Task<string> UploadFileAsync(
        string accessToken, string path, string localFilePath,
        IProgress<double>? progress = null, CancellationToken ct = default);

    Task DownloadFileAsync(
        string accessToken, string path, string destinationPath,
        IProgress<double>? progress = null, CancellationToken ct = default);
}
