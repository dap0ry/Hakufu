using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.MVVM.View;

public partial class ProfileView : UserControl
{
    public ProfileView()
    {
        InitializeComponent();
    }

    // "Guardar imagen": la tarjeta tal cual, al doble de resolución para que
    // se vea nítida al subirla a redes.
    private async void SaveImage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProfileViewModel vm) return;
        var path = await vm.PickImagePathAsync();
        if (path is null) return;

        const double scale = 2;
        var size = Card.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(
            new PixelSize((int)(size.Width * scale), (int)(size.Height * scale)),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(Card);
        bitmap.Save(path);
    }
}
