namespace Hakufu.Services;

/// <summary>
/// Lo que cambia en iPhone/iPad. Lo rellena Platforms/iOS/AppDelegate antes de
/// arrancar Avalonia; en escritorio se queda todo como está (false/null).
/// </summary>
public static class AppPlatform
{
    /// <summary>
    /// iOS/iPadOS: la biblioteca es la carpeta de Hakufu en Archivos (no se elige),
    /// no hay «Salir» y los selectores de archivos trabajan con copias.
    /// </summary>
    public static bool IsMobile { get; set; }

    /// <summary>Abre una carpeta en el gestor de archivos del sistema; null = el de escritorio.</summary>
    public static Action<string>? OpenFolder { get; set; }

    /// <summary>Abre una página https en el navegador; null = el de escritorio.</summary>
    public static Action<string>? OpenUrl { get; set; }
}
