using System.Text.Json.Serialization;

namespace Hakufu.MVVM.Model;

public class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
    [JsonPropertyName("body")]     public string Body    { get; set; } = "";
    [JsonPropertyName("assets")]   public List<GitHubAsset> Assets { get; set; } = [];
}

public class GitHubAsset
{
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}
