namespace Hakufu.MVVM.Model;

public class Collection
{
    public Guid        Id          { get; set; } = Guid.NewGuid();
    public string      Name        { get; set; } = string.Empty;
    // Subcarpeta de la biblioteca de la que sale; "" = tomos sueltos en la raíz ("Sin colección").
    public string?     RelativePath { get; set; }
    public string      Description { get; set; } = string.Empty;
    public List<Guid>  MangaIds    { get; set; } = [];
    public DateTime    CreatedAt   { get; set; } = DateTime.Now;

    // Colección marcada como favorita — se muestra en el perfil.
    public bool         IsFavorite { get; set; } = false;
}
