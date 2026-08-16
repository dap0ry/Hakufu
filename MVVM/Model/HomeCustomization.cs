namespace Hakufu.MVVM.Model;

// Personalización visual del menú de Inicio — 100% local, nunca se sincroniza
// con Drive ni con la cuenta. Las rutas apuntan a copias guardadas en
// %APPDATA%\Hakufu\customization\ (ver CustomizationService), no al archivo
// original que eligió el usuario.
public class HomeCustomization
{
    public string? LeftPanelBackgroundPath { get; set; }

    // Claves: "library", "profile", "friends", "settings", "help", "account".
    public Dictionary<string, string> NavIconPaths       { get; set; } = [];
    public Dictionary<string, string> NavBackgroundPaths { get; set; } = [];
}
