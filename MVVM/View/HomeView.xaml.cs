using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Hakufu.MVVM.View;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
