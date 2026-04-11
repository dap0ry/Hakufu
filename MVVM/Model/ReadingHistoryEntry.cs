namespace Hakufu.MVVM.Model;

public class ReadingHistoryEntry
{
    public Guid     MangaId     { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.Now;
}
