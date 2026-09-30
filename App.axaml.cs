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
            _repo.LoadAsync().GetAwaiter().GetResult();

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
        _repo.SaveAsync().GetAwaiter().GetResult();
    }
}
