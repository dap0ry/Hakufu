using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hakufu.Services;

public class HakufuApiClient
{
    private const string BaseUrl = "http://localhost:8000";
    private readonly ISessionService _session;
    private readonly HttpClient      _http = new() { BaseAddress = new Uri(BaseUrl) };

    // ── Public data records ──────────────────────────────────────────────────
    public record AuthResult(string Username, string AccessToken);
    public record FriendData(string Username, string AvatarUrl);
    public record FriendRequestData(string From, string Id, string AvatarUrl);
    public record CurrentlyReadingData(string MangaTitle, string MangaCoverUrl, int CurrentPage, int TotalPages);
    public record ReadingHistoryData(string MangaTitle, string MangaCoverUrl, DateTime CompletedAt);
    public record PublicProfileData(
        string Username, string Bio, string AvatarUrl, DateTime CreatedAt,
        int MangasCount, int TotalUsageSeconds,
        List<ReadingHistoryData> ReadingHistory,
        CurrentlyReadingData? CurrentlyReading);

    // ── Sync records ────────────────────────────────────────────────────────
    public record MangaSyncItem(
        [property: JsonPropertyName("id")]                   string   Id,
        [property: JsonPropertyName("title")]                string   Title,
        [property: JsonPropertyName("total_pages")]          int      TotalPages,
        [property: JsonPropertyName("cover_cloudinary_url")] string   CoverCloudinaryUrl,
        [property: JsonPropertyName("date_added")]           DateTime DateAdded);

    public record CollectionSyncItem(
        [property: JsonPropertyName("id")]          string       Id,
        [property: JsonPropertyName("name")]        string       Name,
        [property: JsonPropertyName("description")] string       Description,
        [property: JsonPropertyName("manga_ids")]   List<string> MangaIds,
        [property: JsonPropertyName("created_at")]  DateTime     CreatedAt);

    public record ProgressSyncItem(
        [property: JsonPropertyName("manga_id")]     string   MangaId,
        [property: JsonPropertyName("current_page")] int      CurrentPage,
        [property: JsonPropertyName("last_read")]    DateTime LastRead);

    public record HistorySyncItem(
        [property: JsonPropertyName("manga_id")]       string   MangaId,
        [property: JsonPropertyName("manga_title")]    string   MangaTitle,
        [property: JsonPropertyName("manga_cover_url")] string  MangaCoverUrl,
        [property: JsonPropertyName("completed_at")]   DateTime CompletedAt);

    public record LibrarySyncPayload(
        [property: JsonPropertyName("mangas")]            List<MangaSyncItem>     Mangas,
        [property: JsonPropertyName("collections")]       List<CollectionSyncItem> Collections,
        [property: JsonPropertyName("reading_progress")] List<ProgressSyncItem>   ReadingProgress,
        [property: JsonPropertyName("reading_history")]  List<HistorySyncItem>    ReadingHistory,
        [property: JsonPropertyName("total_usage_seconds")] long                  TotalUsageSeconds);

    // ── Private request bodies ───────────────────────────────────────────────
    private record LoginBody(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("password")] string Password);
    private record RegisterBody(
        [property: JsonPropertyName("username")]         string Username,
        [property: JsonPropertyName("email")]            string Email,
        [property: JsonPropertyName("password")]         string Password,
        [property: JsonPropertyName("password_confirm")] string PasswordConfirm);
    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("username")]     string Username);

    public HakufuApiClient(ISessionService session) => _session = session;

    // ── Helpers ──────────────────────────────────────────────────────────────
    private HttpRequestMessage AuthReq(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (_session.Token is { } t)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
        return req;
    }

    private static async Task<string> Detail(HttpResponseMessage r)
    {
        try
        {
            var body = await r.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("detail").GetString() ?? "Error";
        }
        catch { return "Error desconocido"; }
    }

    // ── Auth ─────────────────────────────────────────────────────────────────
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("/auth/login", new LoginBody(username, password));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
        var r = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return new AuthResult(r!.Username, r.AccessToken);
    }

    public async Task<AuthResult> RegisterAsync(string username, string email, string password, string confirm)
    {
        var resp = await _http.PostAsJsonAsync("/auth/register", new RegisterBody(username, email, password, confirm));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
        var r = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return new AuthResult(r!.Username, r.AccessToken);
    }

    // ── Friends ───────────────────────────────────────────────────────────────
    public async Task<List<FriendData>> GetFriendsAsync()
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Get, "/friends"));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
        var list = await resp.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
        return list.Select(e => new FriendData(
            e.GetProperty("username").GetString()  ?? "",
            e.TryGetProperty("avatar_url", out var av) ? av.GetString() ?? "" : "")).ToList();
    }

    public async Task<List<FriendRequestData>> GetPendingRequestsAsync()
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Get, "/friends/requests"));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
        var list = await resp.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
        return list.Select(e => new FriendRequestData(
            e.GetProperty("from").GetString() ?? "",
            e.GetProperty("id").GetString()   ?? "",
            e.TryGetProperty("avatar_url", out var av) ? av.GetString() ?? "" : "")).ToList();
    }

    public async Task SendFriendRequestAsync(string username)
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Post, $"/friends/{username}/request"));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
    }

    public async Task AcceptFriendRequestAsync(string username)
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Put, $"/friends/{username}/accept"));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
    }

    public async Task RejectFriendRequestAsync(string username)
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Delete, $"/friends/{username}/request"));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
    }

    public async Task RemoveFriendAsync(string username)
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Delete, $"/friends/{username}"));
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
    }

    // ── Sync ─────────────────────────────────────────────────────────────────
    public async Task<string> UploadAvatarAsync(byte[] imageBytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var img = new ByteArrayContent(imageBytes);
        img.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(img, "file", "avatar");
        var req = AuthReq(HttpMethod.Post, "/users/me/avatar");
        req.Content = form;
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("avatar_url").GetString() ?? "";
    }

    public async Task<string> UploadCoverAsync(string collectionSlug, string mangaSlug,
                                               string mangaId, byte[] imageBytes)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(mangaId), "manga_id");
        var img = new ByteArrayContent(imageBytes);
        img.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(img, "file", "cover.jpg");

        var req = AuthReq(HttpMethod.Post, $"/users/me/cover/{collectionSlug}/{mangaSlug}");
        req.Content = form;
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("cover_url").GetString() ?? "";
    }

    public async Task SyncUploadAsync(LibrarySyncPayload payload)
    {
        var req = AuthReq(HttpMethod.Put, "/users/me/library");
        req.Content = JsonContent.Create(payload);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
    }

    public async Task<LibrarySyncPayload?> SyncDownloadAsync()
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Get, "/users/me/library"));
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));
        return await resp.Content.ReadFromJsonAsync<LibrarySyncPayload>();
    }

    // ── Public profile ────────────────────────────────────────────────────────
    public async Task<PublicProfileData?> GetPublicProfileAsync(string username)
    {
        var resp = await _http.SendAsync(AuthReq(HttpMethod.Get, $"/users/{username}"));
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await Detail(resp));

        var root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        // Reading history
        var history = new List<ReadingHistoryData>();
        if (root.TryGetProperty("reading_history", out var ha))
            foreach (var h in ha.EnumerateArray())
                history.Add(new ReadingHistoryData(
                    h.TryGetProperty("manga_title",     out var mt)  ? mt.GetString()  ?? "" : "",
                    h.TryGetProperty("manga_cover_url", out var mcu) ? mcu.GetString() ?? "" : "",
                    h.TryGetProperty("completed_at",    out var ca) && ca.TryGetDateTime(out var caDt) ? caDt : DateTime.UtcNow));

        // Currently reading
        CurrentlyReadingData? cr = null;
        if (root.TryGetProperty("currently_reading", out var cre) && cre.ValueKind != JsonValueKind.Null)
            cr = new CurrentlyReadingData(
                cre.TryGetProperty("manga_title",     out var cmt)  ? cmt.GetString()  ?? "" : "",
                cre.TryGetProperty("manga_cover_url", out var cmcu) ? cmcu.GetString() ?? "" : "",
                cre.TryGetProperty("current_page",    out var cp)   ? cp.GetInt32()   : 0,
                cre.TryGetProperty("total_pages",     out var tp)   ? tp.GetInt32()   : 0);

        var createdAt = root.TryGetProperty("created_at", out var cael) && cael.TryGetDateTime(out var cdt) ? cdt : DateTime.UtcNow;

        return new PublicProfileData(
            root.GetProperty("username").GetString() ?? username,
            root.TryGetProperty("bio",                  out var bio) ? bio.GetString()  ?? "" : "",
            root.TryGetProperty("avatar_url",           out var av)  ? av.GetString()   ?? "" : "",
            createdAt,
            root.TryGetProperty("mangas_count",         out var mc)  ? mc.GetInt32()    : 0,
            root.TryGetProperty("total_usage_seconds",  out var tus) ? tus.GetInt32()   : 0,
            history, cr);
    }
}
