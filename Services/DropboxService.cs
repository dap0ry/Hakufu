using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Hakufu.Services;

public class DropboxService : IDropboxService
{
    private const string ContentApiUrl = "https://content.dropboxapi.com/2";
    private const int    ChunkSize     = 8 * 1024 * 1024; // 8 MB por chunk

    private readonly ISessionService _session;
    private readonly HttpClient _api  = new() { BaseAddress = new Uri(HakufuApiClient.BaseUrl) };
    private readonly HttpClient _http = new();

    public DropboxService(ISessionService session) => _session = session;

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
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Get, "dropbox/status"));
        if (!resp.IsSuccessStatusCode) return false;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("connected", out var c) && c.GetBoolean();
    }

    public async Task<string> StartConnectFlowAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Post, "dropbox/link-start"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo iniciar la conexión con Dropbox.");

        var doc     = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var linkUrl = doc.RootElement.GetProperty("link_url").GetString()!;
        if (!linkUrl.StartsWith("https://", StringComparison.Ordinal))
            throw new InvalidOperationException("El enlace de conexión con Dropbox no es válido.");
        Process.Start(new ProcessStartInfo(linkUrl) { UseShellExecute = true });
        return linkUrl;
    }

    public async Task DisconnectAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Post, "dropbox/disconnect"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo desconectar Dropbox.");
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Get, "dropbox/token"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Dropbox no está conectado o la conexión expiró. Conéctalo de nuevo.");

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    // Los valores de la cabecera Dropbox-API-Arg deben ser ASCII de 7 bits —
    // los títulos de manga pueden llevar tildes/ñ. System.Text.Json ya escapa
    // por defecto todo lo no-ASCII como \uXXXX, que es exactamente el formato
    // "header safe" que exige Dropbox — no hace falta (ni es correcto) hacer
    // también percent-encoding con Uri.EscapeDataString, que codificaría
    // también los caracteres estructurales del JSON ({, ", :, /) y rompería
    // el parseo en el lado de Dropbox.
    private static string ArgHeader(object args) => JsonSerializer.Serialize(args);

    // ── Dropbox (directo, con el access token) — subida por sesión/chunks ──
    // Siempre por sesión, nunca el endpoint simple de /upload (limitado a
    // 150 MB) — algunos PDF de manga pueden superarlo.
    public async Task<string> UploadFileAsync(
        string accessToken, string path, string localFilePath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(localFilePath);
        var total = fs.Length;
        var buffer = new byte[ChunkSize];

        // 1. Empezar la sesión con el primer chunk.
        int firstRead = await ReadFullyAsync(fs, buffer, ct);
        var startReq = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/upload_session/start");
        startReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        startReq.Headers.Add("Dropbox-API-Arg", ArgHeader(new { close = false }));
        startReq.Content = new ByteArrayContent(buffer, 0, firstRead);
        startReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var startResp = await _http.SendAsync(startReq, ct);
        await EnsureDropboxSuccessAsync(startResp);
        var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var sessionId = startDoc.RootElement.GetProperty("session_id").GetString()!;

        long sent = firstRead;
        progress?.Report(total > 0 ? (double)sent / total * 100 : 100);

        // 2. Añadir el resto en chunks.
        int read;
        while ((read = await ReadFullyAsync(fs, buffer, ct)) > 0)
        {
            var appendReq = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/upload_session/append_v2");
            appendReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            appendReq.Headers.Add("Dropbox-API-Arg", ArgHeader(new
            {
                cursor = new { session_id = sessionId, offset = sent },
                close = false,
            }));
            appendReq.Content = new ByteArrayContent(buffer, 0, read);
            appendReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var appendResp = await _http.SendAsync(appendReq, ct);
            await EnsureDropboxSuccessAsync(appendResp);

            sent += read;
            progress?.Report(total > 0 ? (double)sent / total * 100 : 100);
        }

        // 3. Cerrar la sesión y guardar el archivo en la ruta final.
        var finishReq = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/upload_session/finish");
        finishReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        finishReq.Headers.Add("Dropbox-API-Arg", ArgHeader(new
        {
            cursor = new { session_id = sessionId, offset = sent },
            commit = new { path, mode = "add", autorename = true },
        }));
        finishReq.Content = new ByteArrayContent([]);
        finishReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var finishResp = await _http.SendAsync(finishReq, ct);
        await EnsureDropboxSuccessAsync(finishResp);
        var finishDoc = JsonDocument.Parse(await finishResp.Content.ReadAsStringAsync());
        return finishDoc.RootElement.GetProperty("path_lower").GetString()!;
    }

    // Dropbox devuelve el motivo real del fallo en el cuerpo (campo
    // error_summary), no en el status code — EnsureSuccessStatusCode() por sí
    // solo da "409 (Conflict)" sin decir el porqué.
    private static async Task EnsureDropboxSuccessAsync(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        string body;
        try { body = await resp.Content.ReadAsStringAsync(); }
        catch { body = ""; }
        throw new InvalidOperationException(
            $"Dropbox devolvió {(int)resp.StatusCode}: {(string.IsNullOrEmpty(body) ? resp.ReasonPhrase : body)}");
    }

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    public async Task DownloadFileAsync(
        string accessToken, string path, string destinationPath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/download");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Add("Dropbox-API-Arg", ArgHeader(new { path }));

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureDropboxSuccessAsync(resp);

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
}
