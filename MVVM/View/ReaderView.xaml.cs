using System.Windows.Controls;
using System.Windows.Input;

namespace Hakufu.MVVM.View;

public partial class ReaderView : UserControl
{
    public ReaderView()
    {
        InitializeComponent();
        // Ensure the control can receive keyboard focus for InputBindings
        Loaded += (_, _) => Focus();
    }

    private void PageArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus(); // keep keyboard focus in the reader after clicking
    }
}
