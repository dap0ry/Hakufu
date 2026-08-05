namespace Hakufu.Services;

public interface IGoogleDriveService
{
    Task<bool> IsConnectedAsync();

    // Pide un link de conexión al backend y lo abre en el navegador del sistema.
    Task<string> StartConnectFlowAsync();

    Task DisconnectAsync();

    // Access token de Google de corta duración, listo para llamar a googleapis.com directamente.
    Task<string> GetAccessTokenAsync();

    Task<string> FindOrCreateBackupFolderAsync(string accessToken);

    // parentId null = raíz de Drive (o de la carpeta "Hakufu" cuando se usa
    // para las subcarpetas de colección).
    Task<string> FindOrCreateFolderAsync(string accessToken, string name, string? parentId);

    // Necesario al restaurar: recuperamos la extensión original (.pdf/.cbz/.cbr)
    // a partir del nombre del archivo en Drive, en vez de adivinarla.
    Task<string?> GetFileNameAsync(string accessToken, string fileId);

    Task<string> UploadFileAsync(
        string accessToken, string parentFolderId, string fileName, string mimeType,
        string localFilePath, IProgress<double>? progress = null, CancellationToken ct = default);

    Task DownloadFileAsync(
        string accessToken, string fileId, string destinationPath,
        IProgress<double>? progress = null, CancellationToken ct = default);
}
