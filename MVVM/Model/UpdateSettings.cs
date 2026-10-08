namespace Hakufu.MVVM.Model;

/// <summary>Ajustes de actualizaciones (Ajustes → Acerca de).</summary>
public class UpdateSettings
{
    /// <summary>Buscar versión nueva al abrir Hakufu.</summary>
    public bool CheckOnStartup { get; set; } = true;
}
