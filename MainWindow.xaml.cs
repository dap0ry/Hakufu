using System.Windows;
using System.Windows.Input;
using Hakufu.MVVM.ViewModel;

namespace Hakufu;

public partial class MainWindow : Window
{
    private bool _isExiting;

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

    private void Reader_ZenModeChanged(object? sender, bool isZen)
    {
        if (isZen)
        {
            TitleBarBorder.Visibility = Visibility.Collapsed;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
        else
        {
            TitleBarBorder.Visibility = Visibility.Visible;
            WindowState = WindowState.Normal;
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
