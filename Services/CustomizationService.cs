using System.IO;

namespace Hakufu.Services;

// Guarda las imágenes de personalización del menú (fondo del panel, iconos y
// fondos de cada botón) copiadas en %APPDATA%\Hakufu\customization\ — igual
// que CoverService cachea las portadas — así no depende de que el archivo
// original siga existiendo donde el usuario lo eligió. Nada de esto se sube
// a ningún sitio; slotKey solo identifica QUÉ se está personalizando
// ("panel.left", "nav.library.icon", …), no contiene datos del usuario.
public class CustomizationService : ICustomizationService
{
    private static readonly string CustomizationDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Hakufu", "customization");

    public string SaveImage(string sourceFilePath, string slotKey)
    {
        Directory.CreateDirectory(CustomizationDir);

        // Por si el slot ya tenía una imagen con otra extensión.
        RemoveImage(slotKey);

        var ext = Path.GetExtension(sourceFilePath);
        var destPath = Path.Combine(CustomizationDir, $"{slotKey}{ext}");
        File.Copy(sourceFilePath, destPath, overwrite: true);
        return destPath;
    }

    public void RemoveImage(string slotKey)
    {
        if (!Directory.Exists(CustomizationDir)) return;

        foreach (var existing in Directory.GetFiles(CustomizationDir, $"{slotKey}.*"))
        {
            try { File.Delete(existing); } catch { /* no bloquea la operación */ }
        }
    }
}
