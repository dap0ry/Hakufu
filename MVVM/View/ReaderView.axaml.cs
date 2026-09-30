using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Hakufu.MVVM.ViewModel;

namespace Hakufu.MVVM.View;

public partial class ReaderView : UserControl
{
    private TopLevel? _topLevel;

    public ReaderView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // El teclado se escucha en la ventana (en túnel) mientras el lector
        // está visible: funciona aunque el foco se haya perdido al pasar a
        // pantalla completa (modo zen) o tras pulsar un botón de la barra.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, TopLevel_KeyDown, RoutingStrategies.Tunnel);

        Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, TopLevel_KeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void TopLevel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || DataContext is not ReaderViewModel vm) return;
        if (e.KeyModifiers != KeyModifiers.None) return;

        ICommand? command = e.Key switch
        {
            Key.Right or Key.Space => vm.NextPageCommand,
            Key.Left               => vm.PrevPageCommand,
            Key.Escape             => vm.ExitZenModeCommand,
            Key.F or Key.F11       => vm.ToggleZenModeCommand,
            Key.D1 or Key.D2       => vm.ToggleTwoPageCommand,
            _                      => null,
        };
        if (command is null) return;

        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }

    private void PageArea_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus(); // mantener el foco en el lector tras hacer clic en la página
    }
}
