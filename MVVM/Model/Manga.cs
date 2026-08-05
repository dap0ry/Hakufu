namespace Hakufu.MVVM.Model;

public class Manga
{
    public Guid   Id             { get; set; } = Guid.NewGuid();
    public string Title          { get; set; } = string.Empty;
    public string FilePath       { get; set; } = string.Empty;
    public string CoverCachePath { get; set; } = string.Empty;
    public int    TotalPages     { get; set; }
    public DateTime DateAdded          { get; set; } = DateTime.Now;
    public string   CloudinaryCoverUrl { get; set; } = string.Empty;

    // Id del archivo en Google Drive una vez respaldado (ver BackupViewModel).
    // Vacío = todavía no se ha subido.
    public string   DriveFileId        { get; set; } = string.Empty;
}
