namespace Hakufu.Services;

public interface ISessionService
{
    string? Username   { get; }
    string? Token      { get; }
    string? AvatarUrl  { get; }
    bool    IsLoggedIn { get; }

    void SetSession(string username, string token, string? avatarUrl = null);
    void ClearSession();
}
