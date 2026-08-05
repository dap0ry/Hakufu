using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Hakufu.Services;

public class GoogleDriveService : IGoogleDriveService
{
    private const string DriveApiUrl      = "https://www.googleapis.com/drive/v3";
    private const string DriveUploadUrl   = "https://www.googleapis.com/upload/drive/v3";
    private const string BackupFolderName = "Hakufu";

    private readonly ISessionService _session;
    private readonly HttpClient _api  = new() { BaseAddress = new Uri(HakufuApiClient.BaseUrl) };
    private readonly HttpClient _http = new();

    public GoogleDriveService(ISessionService session) => _session = session;

    // ── Backend (nuestra API) ───────────────────────────────────────────────
    private HttpRequestMessage AuthReq(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (_session.Token is { } t)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
        return req;
    }

    public async Task<bool> IsConnectedAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Get, "drive/status"));
        if (!resp.IsSuccessStatusCode) return false;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("connected", out var c) && c.GetBoolean();
    }

    public async Task<string> StartConnectFlowAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Post, "drive/link-start"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo iniciar la conexión con Google Drive.");

        var doc     = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var linkUrl = doc.RootElement.GetProperty("link_url").GetString()!;
        Process.Start(new ProcessStartInfo(linkUrl) { UseShellExecute = true });
        return linkUrl;
    }

    public async Task DisconnectAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Post, "drive/disconnect"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo desconectar Google Drive.");
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Get, "drive/token"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Google Drive no está conectado o la conexión expiró. Conéctalo de nuevo.");

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    // ── Google Drive (directo, con el access token) ─────────────────────────
    public Task<string> FindOrCreateBackupFolderAsync(string accessToken) =>
        FindOrCreateFolderAsync(accessToken, BackupFolderName, parentId: null);

    // Busca (o crea) una carpeta por nombre, opcionalmente dentro de otra
    // carpeta (parentId). Se usa tanto para la carpeta raíz "Hakufu" como para
    // la subcarpeta de cada colección dentro de ella.
    public async Task<string> FindOrCreateFolderAsync(string accessToken, string name, string? parentId)
    {
        var escapedName = name.Replace("\\", "\\\\").Replace("'", "\\'");
        var query = $"name='{escapedName}' and mimeType='application/vnd.google-apps.folder' and trashed=false";
        if (parentId is not null) query += $" and '{parentId}' in parents";

        var listReq = new HttpRequestMessage(HttpMethod.Get,
            $"{DriveApiUrl}/files?q={Uri.EscapeDataString(query)}&fields=files(id,name)");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var listResp = await _http.SendAsync(listReq);
        listResp.EnsureSuccessStatusCode();
        var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var files   = listDoc.RootElement.GetProperty("files");
        if (files.GetArrayLength() > 0)
            return files[0].GetProperty("id").GetString()!;

        var createReq = new HttpRequestMessage(HttpMethod.Post, $"{DriveApiUrl}/files");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createReq.Content = JsonContent.Create(new
        {
            name,
            mimeType = "application/vnd.google-apps.folder",
            parents = parentId is not null ? new[] { parentId } : null,
        });

        var createResp = await _http.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();
        var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        return createDoc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<string?> GetFileNameAsync(string accessToken, string fileId)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{DriveApiUrl}/files/{fileId}?fields=name");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
    }

    public async Task<string> UploadFileAsync(
        string accessToken, string parentFolderId, string fileName, string mimeType,
        string localFilePath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        // 1. Iniciar sesión de subida resumable (nos da una URL de subida de un solo uso).
        var metadata = JsonSerializer.Serialize(new { name = fileName, parents = new[] { parentFolderId } });
        var initReq  = new HttpRequestMessage(HttpMethod.Post, $"{DriveUploadUrl}/files?uploadType=resumable");
        initReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        initReq.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
        initReq.Content.Headers.Add("X-Upload-Content-Type", mimeType);

        var initResp = await _http.SendAsync(initReq, ct);
        initResp.EnsureSuccessStatusCode();
        var uploadUrl = initResp.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Google Drive no devolvió una URL de subida.");

        // 2. Subir el contenido del archivo a esa URL.
        await using var fs = File.OpenRead(localFilePath);
        using var content  = new ProgressStreamContent(fs, fs.Length, progress);
        var putReq = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content };
        var putResp = await _http.SendAsync(putReq, ct);
        putResp.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await putResp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task DownloadFileAsync(
        string accessToken, string fileId, string destinationPath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{DriveApiUrl}/files/{fileId}?alt=media");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var total  = resp.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long read  = 0;

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var src  = await resp.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(destinationPath);

        int bytesRead;
        while ((bytesRead = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            read += bytesRead;
            if (total > 0) progress?.Report((double)read / total * 100);
        }
    }

    // Wraps a FileStream so uploading it through HttpClient can report progress —
    // HttpClient has no built-in upload progress, only download (via ReadAsStreamAsync).
    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _stream;
        private readonly long _totalBytes;
        private readonly IProgress<double>? _progress;

        public ProgressStreamContent(Stream stream, long totalBytes, IProgress<double>? progress)
        {
            _stream     = stream;
            _totalBytes = totalBytes;
            _progress   = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[81920];
            long sent  = 0;
            int read;
            while ((read = await _stream.ReadAsync(buffer)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read));
                sent += read;
                if (_totalBytes > 0) _progress?.Report((double)sent / _totalBytes * 100);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return _totalBytes >= 0;
        }
    }
}
