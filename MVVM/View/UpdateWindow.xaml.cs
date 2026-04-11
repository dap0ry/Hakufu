using System.Windows;

namespace Hakufu.MVVM.View;

public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();
    }

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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
