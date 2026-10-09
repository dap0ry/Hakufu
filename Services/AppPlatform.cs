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

    /// <summary>
    /// Elige una foto de la galería (iOS: Fotos) y devuelve la ruta de una copia
    /// local, o null si se cancela; null = el selector de archivos de siempre.
    /// </summary>
    public static Func<Task<string?>>? PickPhotoAsync { get; set; }

    /// <summary>
    /// Android: al dibujar debajo de las barras del sistema, Android (hasta la 14) les pone un
    /// velo oscuro y se ven grises sobre el blanco de la app. Se llama justo después de pedir
    /// dibujar debajo; null = nada que hacer.
    /// </summary>
    public static Action? ClearSystemBarScrim { get; set; }

    /// <summary>
    /// Android: iconos de las barras del sistema claros (true: lo de debajo es oscuro, el lector o
    /// el tema oscuro) u oscuros (false). Lo llama MainView; null = nada que hacer.
    /// </summary>
    public static Action<bool>? SetSystemBarsOverDark { get; set; }

    /// <summary>Android: si Hakufu puede leer el almacenamiento (acceso a todos los archivos); null = no hace falta.</summary>
    public static Func<bool>? HasLibraryAccess { get; set; }

    /// <summary>Android: abre el ajuste del sistema para dar ese permiso.</summary>
    public static Action? RequestLibraryAccess { get; set; }
}
