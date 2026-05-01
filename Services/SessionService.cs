using System.IO;
using System.Text.Json;

namespace Hakufu.Services;

public class SessionService : ISessionService
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "Hakufu", "session.json");

    private string? _username;
    private string? _token;
    private string? _avatarUrl;

    public string? Username   => _username;
    public string? Token      => _token;
    public string? AvatarUrl  => _avatarUrl;
    public bool    IsLoggedIn => _username is not null && _token is not null;

    public SessionService() => TryLoad();

    public void SetSession(string username, string token, string? avatarUrl = null)
    {
        _username  = username;
        _token     = token;
        _avatarUrl = avatarUrl;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new { username, token, avatarUrl }));
    }

    public void ClearSession()
    {
        _username = null;
        _token    = null;
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    private void TryLoad()
    {
        if (!File.Exists(FilePath)) return;
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            _username  = doc.RootElement.GetProperty("username").GetString();
            _token     = doc.RootElement.GetProperty("token").GetString();
            _avatarUrl = doc.RootElement.TryGetProperty("avatarUrl", out var av) ? av.GetString() : null;
        }
        catch { ClearSession(); }
    }
}
