namespace Hakufu.MVVM.Model;

public class Manga
{
    public Guid   Id             { get; set; } = Guid.NewGuid();
    public string Title          { get; set; } = string.Empty;
    // Ruta dentro de la carpeta de la biblioteca ("Colección/Tomo 1.cbz", con
    // '/'): es lo que identifica al tomo entre lecturas de la carpeta.
    public string RelativePath   { get; set; } = string.Empty;
    // Ruta absoluta = raíz de la biblioteca + RelativePath (la recalcula LibraryScanner).
    public string FilePath       { get; set; } = string.Empty;
    public string CoverCachePath { get; set; } = string.Empty;
    public int    TotalPages     { get; set; }
    public DateTime DateAdded          { get; set; } = DateTime.Now;

    // Posición cuando el orden de la colección es "personalizado" (ver
    // ReorderMangaViewModel). Sin usar en los demás modos de orden.
    public int      CustomOrder        { get; set; } = 0;

    // Favorito marcado con la estrellita junto al título. Independiente de
    // los favoritos por colección (ver Collection.IsFavorite).
    public bool     IsFavorite         { get; set; } = false;

    // Momento en el que se marcó como favorito — usado para ordenar el
    // "Top 3" del perfil (más reciente primero). Null si nunca se marcó.
    public DateTime? FavoritedAt       { get; set; }
}
