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

    public string? Username   => _username;
    public string? Token      => _token;
    public bool    IsLoggedIn => _username is not null && _token is not null;

    public SessionService() => TryLoad();

    public void SetSession(string username, string token)
    {
        _username = username;
        _token    = token;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new { username, token }));
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
            _username = doc.RootElement.GetProperty("username").GetString();
            _token    = doc.RootElement.GetProperty("token").GetString();
        }
        catch { ClearSession(); }
    }
}
