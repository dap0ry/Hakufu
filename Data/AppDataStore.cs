using Hakufu.MVVM.Model;

namespace Hakufu.Data;

public class AppDataStore
{
    public List<Manga>               Mangas          { get; set; } = [];
    public List<Collection>          Collections     { get; set; } = [];
    public List<ReadingProgress>     Progress        { get; set; } = [];
    public List<ReadingHistoryEntry> History         { get; set; } = [];
    public string                    ActiveTheme     { get; set; } = "Light";
    public long                      TotalUsageSeconds { get; set; } = 0;

    // "name" | "date" | "custom" — recordado entre sesiones.
    public string                    LibrarySortMode { get; set; } = "name";

    /// <summary>Carpeta del usuario que es la biblioteca (ver LibraryScanner). Vacía = sin elegir.</summary>
    public string                    LibraryRoot     { get; set; } = string.Empty;

    public UserProfile               Profile         { get; set; } = new();

    /// <summary>Un registro por día con lectura (para las gráficas de Perfil).</summary>
    public List<ReadingDay>          ReadingLog      { get; set; } = [];

    public ReaderSettings            Reader          { get; set; } = new();

    public UpdateSettings            Updates         { get; set; } = new();
}
