namespace Hakufu.Services;

public interface ITrayIconService
{
    // onRestore: click/doble clic en el icono o "Abrir Hakufu" del menú.
    // onExit: "Salir" del menú — debe terminar la app de verdad, no ocultarla.
    void Initialize(Action onRestore, Action onExit);
    void Dispose();
}
