using System.Windows;
using System.Windows.Input;
using Hakufu.MVVM.ViewModel;

namespace Hakufu;

public partial class MainWindow : Window
{
    private bool _isExiting;

    // Tamaño/posición y estado previos a entrar en modo zen, para poder
    // restaurarlos tal cual al salir.
    private double _preZenLeft, _preZenTop, _preZenWidth, _preZenHeight;
    private WindowState _preZenState;

    public MainWindow()
    {
        InitializeComponent();

        // Subscribe to zen mode changes once DataContext is set
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel mvm)
                mvm.PropertyChanged += MainVm_PropertyChanged;
        };

        Closing += Window_Closing;
    }

    // La X de la ventana solo la oculta (queda corriendo en la bandeja del
    // sistema) — el proceso solo termina de verdad si algo llama a
    // RequestExit() primero (menú "Salir" de la bandeja, o Ajustes).
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting) return;
        e.Cancel = true;
        Hide();
    }

    public void RequestExit()
    {
        _isExiting = true;
        Close();
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MainVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentView) &&
            DataContext is MainWindowViewModel mvm &&
            mvm.CurrentView is ReaderViewModel reader)
        {
            reader.ZenModeChanged -= Reader_ZenModeChanged;
            reader.ZenModeChanged += Reader_ZenModeChanged;
        }
    }

    // El modo zen NO usa WindowState.Maximized: con WindowStyle="None" (todo
    // este Window ya lo es) un maximizado real se dimensiona por encima del
    // área de trabajo, así que la barra de tareas de Windows tapa por debajo
    // los controles del lector. En vez de eso, el modo zen se ajusta a mano
    // a SystemParameters.WorkArea — la pantalla completa "de verdad" pero
    // sin invadir el hueco de la barra de tareas.
    private void Reader_ZenModeChanged(object? sender, bool isZen)
    {
        if (isZen)
        {
            _preZenState = WindowState;
            if (WindowState == WindowState.Normal)
            {
                _preZenLeft   = Left;
                _preZenTop    = Top;
                _preZenWidth  = Width;
                _preZenHeight = Height;
            }

            TitleBarBorder.Visibility = Visibility.Collapsed;
            WindowState = WindowState.Normal;
            Left   = SystemParameters.WorkArea.Left;
            Top    = SystemParameters.WorkArea.Top;
            Width  = SystemParameters.WorkArea.Width;
            Height = SystemParameters.WorkArea.Height;
        }
        else
        {
            TitleBarBorder.Visibility = Visibility.Visible;

            if (_preZenState == WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
                Left   = _preZenLeft;
                Top    = _preZenTop;
                Width  = _preZenWidth;
                Height = _preZenHeight;
            }
        }
    }

    // Compensate for the DWM shadow offset when maximized so content
    // is not clipped at the top and the Windows taskbar is not covered.
    private void Window_StateChanged(object? sender, EventArgs e)
    {
        RootGrid.Margin = WindowState == WindowState.Maximized
            ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left,
                            SystemParameters.WindowResizeBorderThickness.Top,
                            SystemParameters.WindowResizeBorderThickness.Right,
                            SystemParameters.WindowResizeBorderThickness.Bottom)
            : new Thickness(0);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainWindowViewModel mvm)
            mvm.CloseModalCommand.Execute(null);
    }
}
