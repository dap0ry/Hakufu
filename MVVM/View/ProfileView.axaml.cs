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

    private void SaveHorizontal_Click(object? sender, RoutedEventArgs e) => _ = SaveAsync(ExportHorizontal, "horizontal");
    private void SaveVertical_Click(object? sender, RoutedEventArgs e)   => _ = SaveAsync(ExportVertical, "vertical");

    private async Task SaveAsync(Control card, string orientation)
    {
        if (DataContext is not ProfileViewModel vm) return;
        var path = await vm.PickImagePathAsync(orientation);
        if (path is null) return;
        using var bitmap = RenderCard(card);
        bitmap.Save(path);
    }

    /// <summary>
    /// Pinta una de las tarjetas de exportar (ya maquetadas al doble de tamaño)
    /// en un PNG del mismo tamaño en píxeles.
    /// </summary>
    public static RenderTargetBitmap RenderCard(Control exportHost)
    {
        var size = exportHost.Bounds.Size;
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)));
        bitmap.Render(exportHost);
        return bitmap;
    }
}
