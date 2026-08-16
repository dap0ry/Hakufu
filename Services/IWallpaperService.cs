namespace Hakufu.Services;

public interface IWallpaperService
{
    // Aplica (o quita, si path es null) el wallpaper general de la app.
    // Sustituye el recurso "AppBackground" a nivel de Application — como
    // casi toda la UI pinta su fondo con {DynamicResource AppBackground},
    // esto basta para que se vea detrás de Ajustes, Biblioteca, etc. sin
    // tocar ninguna vista.
    void Apply(string? imagePath, double opacity);
}
