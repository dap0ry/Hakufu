namespace Hakufu.MVVM.Model;

/// <summary>Preferencias del lector (Ajustes → Lectura). Se guardan en data.json.</summary>
public class ReaderSettings
{
    /// <summary>Abrir los mangas a doble página.</summary>
    public bool TwoPageByDefault { get; set; } = false;

    /// <summary>Abrir los mangas directamente en modo zen (pantalla completa).</summary>
    public bool OpenInZenMode { get; set; } = false;

    /// <summary>Animación 3D al pasar la hoja.</summary>
    public bool PageTurnAnimation { get; set; } = true;

    /// <summary>"fast" | "normal" | "slow"</summary>
    public string PageTurnSpeed { get; set; } = "normal";
}
