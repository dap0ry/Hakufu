using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Hakufu.MVVM.ViewModel;

namespace Hakufu;

public partial class MainWindow : Window
{
    private WindowState _preZenState;
    private ReaderViewModel? _reader;

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
    }

    private void MainVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentView) ||
            DataContext is not MainWindowViewModel mvm) return;

        if (_reader is not null)
        {
            _reader.ZenModeChanged -= Reader_ZenModeChanged;
            _reader = null;
            // Salir del lector estando en zen: volver al tamaño normal.
            if (WindowState == WindowState.FullScreen) WindowState = _preZenState;
        }

        if (mvm.CurrentView is ReaderViewModel reader)
        {
            _reader = reader;
            reader.ZenModeChanged += Reader_ZenModeChanged;
        }
    }

    // Modo zen del lector = pantalla completa nativa del sistema.
    private void Reader_ZenModeChanged(object? sender, bool isZen)
    {
        if (isZen)
        {
            _preZenState = WindowState;
            WindowState  = WindowState.FullScreen;
        }
        else
        {
            WindowState = _preZenState == WindowState.FullScreen ? WindowState.Normal : _preZenState;
        }
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel mvm)
            mvm.CloseModalCommand.Execute(null);
    }
}
