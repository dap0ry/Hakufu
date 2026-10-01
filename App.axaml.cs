using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Hakufu.Data;

namespace Hakufu;

public partial class App : Application
{
    private IDataRepository? _repo;
    private DateTime         _sessionStart;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _sessionStart = DateTime.Now;

            // Se carga de forma síncrona antes de crear la ventana: data.json
            // es pequeño y así la primera pantalla ya sale con la biblioteca.
            _repo = new JsonDataRepository();
            // Task.Run: esperar un async bloqueando el hilo de UI (que ya tiene
            // el SynchronizationContext de Avalonia) es un deadlock en cuanto
            // alguna continuación intente volver a él.
            Task.Run(_repo.LoadAsync).GetAwaiter().GetResult();

            // Restos de "Personalizar" (retirado en la 0.10.1): copias de imágenes
            // que ya no usa nada.
            try { Directory.Delete(Path.Combine(Data.AppPaths.DataDir, "customization"), recursive: true); }
            catch { /* no existe o no se puede borrar: da igual */ }

            var mainWindow = new MainWindow();
            try
            {
                var root = new CompositionRoot(_repo);
                root.ApplySavedAppearance();
                mainWindow.DataContext = root.CreateMainViewModel();
            }
            catch (Exception ex)
            {
                mainWindow.Content = new TextBlock
                {
                    Text = $"Error al iniciar Hakufu:\n{ex.Message}",
                    Margin = new Thickness(24)
                };
            }
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, e) =>
            {
                SaveOnExit();
                // pdfium (Docnet.Core) deja hilos nativos vivos; forzar la salida
                // para que el proceso no se quede colgado tras cerrar.
                Environment.Exit(e.ApplicationExitCode);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SaveOnExit()
    {
        if (_repo is null) return;
        var elapsed = (long)(DateTime.Now - _sessionStart).TotalSeconds;
        _repo.Current.TotalUsageSeconds += elapsed;
        Task.Run(_repo.SaveAsync).GetAwaiter().GetResult(); // ver LoadAsync arriba
    }
}
