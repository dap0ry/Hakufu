using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Hakufu.I18n;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu;

public partial class MainWindow : Window
{
    private WindowState _preZenState;
    private bool _zen;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel mvm)
                mvm.PropertyChanged += MainVm_PropertyChanged;
        };
        MainView.ZenModeChanged += Reader_ZenModeChanged;
        MainView.ReaderClosed   += Reader_Closed;

        foreach (var grip in ResizeGrips.Children)
            grip.PointerPressed += ResizeGrip_PointerPressed;

        UpdateFullScreenTip();
    }

    // Lo que el code-behind escribe a mano (el ToolTip de pantalla completa) se rehace al cambiar de idioma.
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Localizer.Instance.LanguageChanged += UpdateFullScreenTip;
    }

    protected override void OnClosed(EventArgs e)
    {
        Localizer.Instance.LanguageChanged -= UpdateFullScreenTip;
        base.OnClosed(e);
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
        UpdateFullScreenTip();
    }

    private void UpdateFullScreenTip() =>
        ToolTip.SetTip(FullScreenButton,
                       L.Get(WindowState == WindowState.FullScreen ? "shell.exit_fullscreen" : "shell.fullscreen"));

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
        MainView.FlushReader();
        base.OnClosing(e);
    }

    // En el lector la barra de título va oscura, como la del lector.
    private void MainVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentView) && DataContext is MainWindowViewModel mvm)
            TitleBar.Classes.Set("reader", mvm.CurrentView is ReaderViewModel);
    }

    // Salir del lector estando en zen: volver al tamaño de antes (la pantalla
    // completa del botón de la barra, si venía de ahí, se queda).
    private void Reader_Closed(object? sender, bool wasZen)
    {
        _zen = false;
        if (wasZen && WindowState == WindowState.FullScreen) WindowState = _preZenState;
        UpdateChrome();
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
}
