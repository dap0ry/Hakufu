using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Globalization;
using Hakufu.Data;
using Hakufu.I18n;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu;

public partial class App : Application
{
    private IDataRepository? _repo;
    private DateTime         _sessionStart;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                StartDesktop(desktop);
                break;
            case ISingleViewApplicationLifetime single: // iPhone/iPad
                StartSingleView(single);
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        LoadData();
        var mainWindow = new MainWindow { OwnTitleBar = OperatingSystem.IsLinux() };
        try
        {
            var root = Compose();
            mainWindow.DataContext = root.CreateMainViewModel();
            // A partir de aquí, cambiar de tema se anima (el de arranque no).
            root.Theme.Transition = mainWindow.PlayThemeTransition;
            _ = StartBackgroundWork(root);
        }
        catch (Exception ex)
        {
            mainWindow.Content = StartupError(ex);
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

    // iPhone/iPad: MainView es toda la pantalla. iOS no cierra las apps, las
    // suspende: lo que en escritorio se guarda al salir se guarda al pasar a
    // segundo plano.
    private void StartSingleView(ISingleViewApplicationLifetime single)
    {
        LoadData();
        var view = new MainView();
        CompositionRoot? root = null;
        try
        {
            root = Compose();
            view.DataContext = root.CreateMainViewModel();
            root.Theme.Transition = view.PlayThemeTransition;
            var scan = StartBackgroundWork(root);
            _ = OpenStartScreenAsync(root, scan);
        }
        catch (Exception ex)
        {
            view.Content = StartupError(ex);
        }
        single.MainView = view;

        // Modo zen del lector: sin barra de estado.
        view.ZenModeChanged += (_, zen) => SetSystemBarVisible(view, !zen);
        view.ReaderClosed   += (_, _) => SetSystemBarVisible(view, true);
        view.AttachedToVisualTree += (_, _) => FollowSafeArea(view);
        // Botón o gesto de atrás (Android): lo que no resuelve Hakufu lo hace el sistema (salir).
        view.AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(view) is { } top)
                top.BackRequested += (_, e) => e.Handled = view.HandleBack();
        };

        if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
        {
            activatable.Deactivated += (_, e) =>
            {
                if (e.Kind != ActivationKind.Background) return;
                view.FlushReader();
                SaveOnExit();
            };
            // Lo que pasa en segundo plano no es tiempo de uso.
            activatable.Activated += (_, e) =>
            {
                if (e.Kind != ActivationKind.Background) return;
                _sessionStart = DateTime.Now;
                // Al volver (de dar el permiso, de copiar mangas con otra app…), se vuelve a leer la carpeta.
                if (root?.Navigation.CurrentViewModel is LibraryViewModel library) _ = library.RefreshAsync();
                else if (root is not null) _ = root.Scanner.ScanAsync();
            };
        }
    }

    /// <summary>data.json, idioma y limpieza; antes de crear ninguna vista.</summary>
    private void LoadData()
    {
        _sessionStart = DateTime.Now;

        // Se carga de forma síncrona antes de crear la ventana: data.json
        // es pequeño y así la primera pantalla ya sale con la biblioteca.
        _repo = new JsonDataRepository();
        // Task.Run: esperar un async bloqueando el hilo de UI (que ya tiene
        // el SynchronizationContext de Avalonia) es un deadlock en cuanto
        // alguna continuación intente volver a él.
        Task.Run(_repo.LoadAsync).GetAwaiter().GetResult();

        // iOS: si la app se reinstaló, su contenedor puede estar en otra ruta.
        if (AppPlatform.IsMobile && Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home &&
            ContainerPaths.Rebase(_repo.Current, home))
            Task.Run(_repo.SaveAsync).GetAwaiter().GetResult();

        // Idioma: el guardado, o el del sistema la primera vez (ver Localizer.ResolveInitial).
        var store = _repo.Current;
        var hasData = store.Mangas.Count > 0 || store.LibraryRoot != "" ||
                      store.TotalUsageSeconds > 0 || store.ReadingLog.Count > 0;
        store.Language = Localizer.ResolveInitial(store.Language, hasData, CultureInfo.CurrentUICulture);
        Localizer.Instance.SetLanguage(store.Language);

        // Restos de "Personalizar" (retirado en la 0.10.1): copias de imágenes
        // que ya no usa nada.
        try { Directory.Delete(Path.Combine(Data.AppPaths.DataDir, "customization"), recursive: true); }
        catch { /* no existe o no se puede borrar: da igual */ }
    }

    private CompositionRoot Compose()
    {
        var root = new CompositionRoot(_repo!);
        root.ApplySavedAppearance();
        return root;
    }

    /// <returns>La primera lectura de la carpeta de la biblioteca.</returns>
    private Task StartBackgroundWork(CompositionRoot root)
    {
        // Aplicar una actualización cierra el proceso sin pasar por desktop.Exit.
        root.PrepareRestart = SaveOnExit;
        _ = root.UpdateBanner.StartAsync();
        // La carpeta de la biblioteca puede haber cambiado con Hakufu cerrado.
        return root.Scanner.ScanAsync();
    }

    // HAKUFU_START_SCREEN (capturas del CI en el simulador): cuando la biblioteca ya está leída.
    private static async Task OpenStartScreenAsync(CompositionRoot root, Task scan)
    {
        await scan;
        StartScreen.Apply(root);
    }

    private static TextBlock StartupError(Exception ex) => new()
    {
        Text = $"Error al iniciar Hakufu:\n{ex.Message}",
        Margin = new Thickness(24)
    };

    private static void SetSystemBarVisible(Visual view, bool visible)
    {
        if (TopLevel.GetTopLevel(view)?.InsetsManager is { } insets) insets.IsSystemBarVisible = visible;
    }

    // El contenido se dibuja hasta los bordes y se aparta de la muesca/isla y de la
    // barra de inicio con el margen que da el sistema.
    private static void FollowSafeArea(MainView view)
    {
        if (TopLevel.GetTopLevel(view)?.InsetsManager is not { } insets) return;
        insets.DisplayEdgeToEdgePreference = true;
        view.Padding = insets.SafeAreaPadding;
        insets.SafeAreaChanged += (_, e) => view.Padding = e.SafeAreaPadding;
    }

    private void SaveOnExit()
    {
        if (_repo is null) return;
        var elapsed = (long)(DateTime.Now - _sessionStart).TotalSeconds;
        _repo.Current.TotalUsageSeconds += elapsed;
        _sessionStart = DateTime.Now; // si se llama dos veces (actualizar y luego salir), no cuenta doble
        Task.Run(_repo.SaveAsync).GetAwaiter().GetResult(); // ver LoadAsync arriba
    }
}
