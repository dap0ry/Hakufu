namespace Hakufu.MVVM.Model;

public class Manga
{
    public Guid   Id             { get; set; } = Guid.NewGuid();
    public string Title          { get; set; } = string.Empty;
    public string FilePath       { get; set; } = string.Empty;
    public string CoverCachePath { get; set; } = string.Empty;
    public int    TotalPages     { get; set; }
    public DateTime DateAdded    { get; set; } = DateTime.Now;
}
