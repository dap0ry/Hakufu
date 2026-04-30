namespace Hakufu.Services;

public interface ISessionService
{
    string? Username   { get; }
    string? Token      { get; }
    bool    IsLoggedIn { get; }

    void SetSession(string username, string token);
    void ClearSession();
}
