using System.Windows;
using System.Windows.Input;
using Hakufu.MVVM.ViewModel;

namespace Hakufu;

public partial class MainWindow : Window
{
    // Tamaño/posición previos a "maximizar" (ver EnterFakeMaximize más abajo).
    private double _preMaximizeLeft, _preMaximizeTop, _preMaximizeWidth, _preMaximizeHeight;
    private bool   _isFakeMaximized;

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

    // WindowState.Maximized real, combinado con WindowChrome + WindowStyle="None",
    // tiene un bug conocido de WPF: la ventana se dimensiona más allá del área
    // de trabajo real — tapa la barra de tareas de Windows por debajo, y los
    // botones de arriba quedan un poco por encima del borde visible de la
    // pantalla (parece que se cortan). Aquí "maximizar" nunca usa
    // WindowState.Maximized de verdad — se queda en Normal pero ajustado a
    // mano a SystemParameters.WorkArea, igual que ya hace el modo zen del
    // lector más abajo.
    //
    // Este handler también intercepta cuando WPF/Windows entra en Maximized
    // por su cuenta (doble clic en la barra de título, Win+Flecha arriba —
    // WindowChrome deja pasar esos gestos nativos sin pasar por
    // MaximizeButton_Click) y lo reconduce siempre a EnterFakeMaximize().
    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            EnterFakeMaximize();
    }

    private void EnterFakeMaximize()
    {
        if (!_isFakeMaximized)
        {
            // RestoreBounds es la fuente fiable del tamaño/posición previos
            // cuando se llega aquí ya en Maximized (p. ej. doble clic en la
            // barra de título) — Left/Top/Width/Height no se actualizan de
            // forma consistente en ese caso.
            var restore = WindowState == WindowState.Maximized
                ? RestoreBounds
                : new Rect(Left, Top, Width, Height);
            _preMaximizeLeft   = restore.Left;
            _preMaximizeTop    = restore.Top;
            _preMaximizeWidth  = restore.Width;
            _preMaximizeHeight = restore.Height;
        }

        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;

        Left   = SystemParameters.WorkArea.Left;
        Top    = SystemParameters.WorkArea.Top;
        Width  = SystemParameters.WorkArea.Width;
        Height = SystemParameters.WorkArea.Height;
        _isFakeMaximized = true;
    }

    private void ExitFakeMaximize()
    {
        Left   = _preMaximizeLeft;
        Top    = _preMaximizeTop;
        Width  = _preMaximizeWidth;
        Height = _preMaximizeHeight;
        _isFakeMaximized = false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFakeMaximized) ExitFakeMaximize();
        else EnterFakeMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainWindowViewModel mvm)
            mvm.CloseModalCommand.Execute(null);
    }
}
