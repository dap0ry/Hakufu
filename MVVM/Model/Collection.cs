namespace Hakufu.MVVM.Model;

public class Collection
{
    public Guid        Id          { get; set; } = Guid.NewGuid();
    public string      Name        { get; set; } = string.Empty;
    public string      Description { get; set; } = string.Empty;
    public List<Guid>  MangaIds    { get; set; } = [];
    public DateTime    CreatedAt   { get; set; } = DateTime.Now;
}
