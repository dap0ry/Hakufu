using System.Windows;
using System.Windows.Input;
using Hakufu.MVVM.ViewModel;

namespace Hakufu;

public partial class MainWindow : Window
{
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
    }

    // La X de la ventana cierra Hakufu del todo — exactamente igual que
    // "Salir de Hakufu" en Ajustes o "Salir" en el menú de la bandeja del
    // sistema. (Antes solo la ocultaba y el proceso seguía corriendo detrás;
    // se quitó porque confundía — parecía que la app no arrancaba en la
    // versión nueva al reabrirla, cuando en realidad seguía siendo la misma
    // sesión de siempre.) ShutdownMode por defecto (OnLastWindowClose) hace
    // que cerrar esta ventana también termine el proceso.
    public void RequestExit() => Close();

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
