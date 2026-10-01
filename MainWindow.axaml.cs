using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu;

public partial class MainWindow : Window
{
    private WindowState _preZenState;
    private ReaderViewModel? _reader;
    private bool _zen;

    public MainWindow()
    {
        InitializeComponent();

        // Equivalente al CommandManager de WPF: tras cada clic o tecla se
        // reevalúa el CanExecute de los comandos (ver CommandRequery).
        AddHandler(PointerReleasedEvent, (_, _) => CommandRequery.RequestInvalidate(),
                   RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, (_, _) => CommandRequery.RequestInvalidate(),
                   RoutingStrategies.Tunnel, handledEventsToo: true);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel mvm)
                mvm.PropertyChanged += MainVm_PropertyChanged;
        };

        foreach (var grip in ResizeGrips.Children)
            grip.PointerPressed += ResizeGrip_PointerPressed;
    }

    // ── Barra de título propia ───────────────────────────────────────────────

    /// <summary>
    /// Barra de título y bordes de Hakufu en vez de las decoraciones del sistema.
    /// La app la pone en Linux: muchos gestores de ventanas (niri, Sway,
    /// Hyprland… con XWayland) no dibujan ninguna y la ventana se quedaba sin
    /// minimizar, maximizar ni cerrar. Fuera de la app (tests, capturas) no.
    /// </summary>
    public bool OwnTitleBar
    {
        get => _ownTitleBar;
        set
        {
            _ownTitleBar = value;
            SystemDecorations = value ? SystemDecorations.None : SystemDecorations.Full;
            UpdateChrome();
        }
    }
    private bool _ownTitleBar;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty) UpdateChrome();
    }

    private void UpdateChrome()
    {
        var fullScreen = WindowState == WindowState.FullScreen;
        // En pantalla completa la barra se queda (es la forma de salir), salvo
        // en el modo zen del lector, que es justo para no ver nada más.
        TitleBar.IsVisible    = _ownTitleBar && !(fullScreen && _zen);
        ResizeGrips.IsVisible = _ownTitleBar && WindowState == WindowState.Normal;

        FullScreenGlyph.Data = Geometry.Parse(fullScreen
            ? "M3.5,0.5 L3.5,3.5 L0.5,3.5 M6.5,0.5 L6.5,3.5 L9.5,3.5 M9.5,6.5 L6.5,6.5 L6.5,9.5 M3.5,9.5 L3.5,6.5 L0.5,6.5"   // salir
            : "M0.5,3.5 L0.5,0.5 L3.5,0.5 M6.5,0.5 L9.5,0.5 L9.5,3.5 M9.5,6.5 L9.5,9.5 L6.5,9.5 M3.5,9.5 L0.5,9.5 L0.5,6.5"); // entrar
        ToolTip.SetTip(FullScreenButton, fullScreen ? "Salir de pantalla completa" : "Pantalla completa");
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) ToggleMaximized();
        else BeginMoveDrag(e);
    }

    private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: string tag } &&
            Enum.TryParse<WindowEdge>(tag, out var edge) &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(edge, e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)   => WindowState = WindowState.Minimized;
    private void FullScreen_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    private void Close_Click(object? sender, RoutedEventArgs e)      => Close();

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // ── Transición de tema ───────────────────────────────────────────────────

    /// <summary>
    /// ThemeService.Transition: foto de la ventana con el tema viejo, cambio de
    /// tema y la veta de tinta destapando el nuevo (ver InkTransition). Va como
    /// el interruptor de Ajustes: a oscuro, de izquierda a derecha; de vuelta a
    /// claro, de derecha a izquierda.
    /// </summary>
    public void PlayThemeTransition(AppTheme theme, Action applyTheme)
    {
        var captured = IsVisible && ThemeInk.Capture(Root);
        applyTheme();
        if (captured) ThemeInk.Play(rightToLeft: theme == AppTheme.Light);
    }

    // Cerrar la app leyendo: apuntar el rato leído antes de que App guarde.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _reader?.FlushReadingTime();
        base.OnClosing(e);
    }

    private void MainVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentView) ||
            DataContext is not MainWindowViewModel mvm) return;

        if (_reader is not null)
        {
            _reader.ZenModeChanged -= Reader_ZenModeChanged;
            // Cierra el documento de pdfium (Docnet no tiene finalizador: sin
            // esto cada PDF abierto se quedaba abierto hasta cerrar la app).
            _reader.Dispose();
            _reader = null;
            // Salir del lector estando en zen: volver al tamaño de antes (la
            // pantalla completa del botón de la barra, si venía de ahí, se queda).
            var wasZen = _zen;
            _zen = false;
            if (wasZen && WindowState == WindowState.FullScreen) WindowState = _preZenState;
            UpdateChrome();
        }

        // En el lector la barra de título va oscura, como la del lector.
        TitleBar.Classes.Set("reader", mvm.CurrentView is ReaderViewModel);

        if (mvm.CurrentView is ReaderViewModel reader)
        {
            _reader = reader;
            reader.ZenModeChanged += Reader_ZenModeChanged;
            if (reader.OpenInZenMode) reader.IsZenMode = true;
        }
    }

    // Modo zen del lector = pantalla completa nativa del sistema.
    private void Reader_ZenModeChanged(object? sender, bool isZen)
    {
        _zen = isZen;
        if (isZen)
        {
            _preZenState = WindowState;
            WindowState  = WindowState.FullScreen;
        }
        else
        {
            WindowState = _preZenState == WindowState.FullScreen ? WindowState.Normal : _preZenState;
        }
        UpdateChrome(); // si ya estaba en pantalla completa, WindowState no cambia
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel mvm)
            mvm.CloseModalCommand.Execute(null);
    }
}
