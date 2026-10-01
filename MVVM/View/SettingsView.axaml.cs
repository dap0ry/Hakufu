using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;

namespace Hakufu.MVVM.View;

public partial class SettingsView : UserControl
{
    private TopLevel? _topLevel;

    public SettingsView() => InitializeComponent();

    // Mientras un atajo está "escuchando", la siguiente tecla (en túnel, antes
    // de que la use ningún botón) es la que se asigna.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, TopLevel_KeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, TopLevel_KeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void TopLevel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || vm.CapturingSlot is null) return;
        if (ShortcutService.IsModifierKey(e.Key)) return; // esperar a la tecla de verdad

        if (vm.AssignCapturedKey(new KeyGesture(e.Key, e.KeyModifiers)))
            _ = vm.SaveShortcutsAsync();
        e.Handled = true;
    }
}
