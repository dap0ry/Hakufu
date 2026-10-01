namespace Hakufu.MVVM.Model;

/// <summary>Lo que sale arriba en la tarjeta de Perfil. Solo local.</summary>
public class UserProfile
{
    public string    Name        { get; set; } = string.Empty;
    /// <summary>Copia de la foto dentro de la carpeta de datos (AppPaths.ProfileDir), o vacío.</summary>
    public string    AvatarPath  { get; set; } = string.Empty;
    public DateTime? MemberSince { get; set; }
    /// <summary>El que sale como "Manga favorito"; si no hay, el primero con estrella.</summary>
    public Guid?     FavoriteMangaId { get; set; }
}

/// <summary>Lo leído un día: tiempo con el lector abierto (sin ratos muertos) y páginas pasadas.</summary>
public class ReadingDay
{
    public DateOnly Date    { get; set; }
    public double   Seconds { get; set; }
    public int      Pages   { get; set; }
}
