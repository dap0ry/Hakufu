namespace Hakufu.MVVM.Model;

public class ReadingProgress
{
    public Guid     MangaId     { get; set; }
    public int      CurrentPage { get; set; }
    public DateTime LastRead    { get; set; } = DateTime.Now;
}
