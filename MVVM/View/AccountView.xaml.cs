using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.MVVM.View;

public partial class AccountView : UserControl
{
    public AccountView() => InitializeComponent();

    private AccountViewModel Vm => (AccountViewModel)DataContext;

    private void LoginPwBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        => Vm.LoginPassword = LoginPwBox.Password;

    private void RegPwBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        => Vm.RegPassword = RegPwBox.Password;

    private void RegPwConfirmBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        => Vm.RegPasswordConfirm = RegPwConfirmBox.Password;

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
