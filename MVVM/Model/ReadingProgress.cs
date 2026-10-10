namespace Hakufu.MVVM.Model;

public class ReadingProgress
{
    public Guid     MangaId     { get; set; }
    public int      CurrentPage { get; set; }
    public DateTime LastRead    { get; set; } = DateTime.Now;
    /// <summary>
    /// CurrentPage cuenta las páginas en orden natural. Falso en el progreso guardado hasta
    /// la 0.12 (orden ordinal): se pasa al abrir el tomo (LibraryService.GetProgressForReading).
    /// </summary>
    public bool     NaturalOrder { get; set; }
}
