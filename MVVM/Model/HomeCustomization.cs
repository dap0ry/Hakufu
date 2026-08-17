namespace Hakufu.MVVM.Model;

// Una imagen de personalización + su opacidad — 100% local, nunca se
// sincroniza con Dropbox ni con la cuenta.
public class CustomizationImage
{
    public string Path    { get; set; } = "";
    public double Opacity { get; set; } = 1.0;
}

// Personalización visual — panel de Inicio, botones de navegación, y el
// wallpaper general que se ve de fondo en toda la app (Ajustes, Biblioteca,
// etc.). Las rutas apuntan a copias guardadas en
// %APPDATA%\Hakufu\customization\ (ver CustomizationService), no al archivo
// original que eligió el usuario.
public class HomeCustomization
{
    public CustomizationImage? LeftPanelBackground { get; set; }

    // Fondo detrás de todas las pantallas (sustituye al recurso AppBackground
    // de todo el tema — ver WallpaperService).
    public CustomizationImage? GeneralWallpaper { get; set; }

    // Claves: "library", "profile", "friends", "settings", "help", "account".
    public Dictionary<string, CustomizationImage> NavIcons       { get; set; } = [];
    public Dictionary<string, CustomizationImage> NavBackgrounds { get; set; } = [];
}
