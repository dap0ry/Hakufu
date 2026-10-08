using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu;

public partial class MainView : UserControl
{
    /// <summary>Por debajo de este ancho (iPhone en vertical) las vistas van en una columna.</summary>
    public const double CompactWidth = 700;

    /// <summary>
    /// Por debajo de este ancho (iPad en vertical, y el iPhone) la clase "narrow". Es el
    /// mínimo de la ventana de escritorio: en escritorio nunca se aplica.
    /// </summary>
    public const double NarrowWidth = 900;

    private MainWindowViewModel? _vm;
    private ReaderViewModel? _reader;

    /// <summary>El lector entra (true) o sale (false) del modo zen.</summary>
    public event EventHandler<bool>? ZenModeChanged;

    /// <summary>Se dejó el lector; el argumento dice si estaba en modo zen.</summary>
    public event EventHandler<bool>? ReaderClosed;

    public MainView()
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
            if (_vm is not null) _vm.PropertyChanged -= MainVm_PropertyChanged;
            _vm = DataContext as MainWindowViewModel;
            if (_vm is not null) _vm.PropertyChanged += MainVm_PropertyChanged;
        };
    }

    /// <summary>El lector abierto, si lo hay.</summary>
    public ReaderViewModel? Reader => _reader;

    public static readonly DirectProperty<MainView, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<MainView, bool>(nameof(IsCompact), v => v.IsCompact);

    private bool _isCompact;

    /// <summary>iPhone en vertical (menos de 700 px): clase "compact" para los estilos de las vistas.</summary>
    public bool IsCompact
    {
        get => _isCompact;
        private set
        {
            if (!SetAndRaise(IsCompactProperty, ref _isCompact, value)) return;
            Classes.Set("compact", value);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        IsCompact = e.NewSize.Width < CompactWidth;
        Classes.Set("narrow", e.NewSize.Width < NarrowWidth);
    }

    /// <summary>Apunta el rato leído (al cerrar la app o pasar a segundo plano).</summary>
    public void FlushReader() => _reader?.FlushReadingTime();

    /// <summary>Como MainWindow.PlayThemeTransition, para cuando MainView es la vista única.</summary>
    public void PlayThemeTransition(AppTheme theme, Action applyTheme)
    {
        var captured = IsVisible && ThemeInk.Capture(Root);
        applyTheme();
        if (captured) ThemeInk.Play(rightToLeft: theme == AppTheme.Light);
    }

    private void MainVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentView) || _vm is null) return;

        if (_reader is not null)
        {
            _reader.ZenModeChanged -= Reader_ZenModeChanged;
            var wasZen = _reader.IsZenMode;
            // Cierra el documento de pdfium (Docnet no tiene finalizador: sin
            // esto cada PDF abierto se quedaba abierto hasta cerrar la app).
            _reader.Dispose();
            _reader = null;
            ReaderClosed?.Invoke(this, wasZen);
        }

        if (_vm.CurrentView is ReaderViewModel reader)
        {
            _reader = reader;
            reader.ZenModeChanged += Reader_ZenModeChanged;
            if (reader.OpenInZenMode) reader.IsZenMode = true;
        }
    }

    private void Reader_ZenModeChanged(object? sender, bool isZen) => ZenModeChanged?.Invoke(this, isZen);

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        => _vm?.CloseModalCommand.Execute(null);
}
