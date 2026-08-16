namespace Hakufu.Services;

public interface ICustomizationService
{
    // Copia sourceFilePath a la caché local de personalización y devuelve la
    // ruta de esa copia (nunca la ruta original).
    string SaveImage(string sourceFilePath, string slotKey);

    // Borra cualquier copia guardada para ese slot, si la hay.
    void RemoveImage(string slotKey);
}
