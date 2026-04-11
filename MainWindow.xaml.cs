using System.Windows;
using System.Windows.Input;
using Hakufu.MVVM.ViewModel;

namespace Hakufu;

public partial class MainWindow : Window
{
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
